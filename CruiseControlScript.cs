const string INCREASE_SPEED_ARGUMENT = "+speed"; 
const string DECREASE_SPEED_ARGUMENT = "-speed"; 
const string TOGGLE_CRUISE_ARGUMENT = "-toggleCruise";
const string PRECISION_MODE_ARGUMENT = "-precisionMode";

const string DEBUG_PANEL_NAME = "[DEBUG] Text Panel";
const string STATUS_PANEL_NAME = "[1] Text panel L";
const string CONTROLLING_TIMER_NAME = "[1] Timer Cruise Control";	//name of the timer that runs this programmable block and triggers itself

const short DEF_DECIMAL_PLACES = 2;
const float TARGET_SPEED_INCREMENT_PER_SECOND = 20.0F;	//The rate at which target speed is changed
const float MAX_SPEED = 100.0F; 		//The maximum speed of the world the script is going to be used in
								//(Or the maximum speed the user wants the ship to move with)
const float MAX_SPEED_DEVIATION = 0.5F; //The maximum deviation that the script will allow between the 
                                                                            //target speed and the actual speed 
const float PRECISION_FACTOR = 0.2F;	//DO NOT SET TO ZERO
				//The precision of factor determines how much slower speed control is under precision mode. 0.1 means 10 times slower
const string FORWARD_THRUSTERS_TAG = "(Forward)"; 
const string BACKWARD_THRUSTERS_TAG = "(Backward)"; 
 
Vector3D gGlobalGridPosition = new Vector3D(0,0,0); 
float gCurrentSpeed = 0.0F;  
float gTargetSpeed;   //The speed which the ship is supposed to cruise with. 
float gTargetSpeedChangePerSecond = 0.0F;	//Current rate of change of the target speed per second
short gCurrentAcceleration = 0;  // State of acceleration (-1 to decelerate, 1 to accelerate, 0 for neither) 
bool gIsCruising = false;	//true if in cruise mode. False otherwise
bool gPrecisionMode = false;	//true if precision mode is on. Precision mode allows for a precise speed control
IMyTextPanel gStatusTextPanel;
IMyTimerBlock gControlTimer;

//Debug variables
IMyTextPanel gDebugTextPanel;





public Program() {
    //===GETTING BLOCKS THAT WILL BE USED IN THE SCRIPT===
    gStatusTextPanel = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(STATUS_PANEL_NAME);
    gControlTimer = (IMyTimerBlock) GridTerminalSystem.GetBlockWithName(CONTROLLING_TIMER_NAME);

    gDebugTextPanel = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(DEBUG_PANEL_NAME);
}
public void Main(string argument) {

	if(argument == TOGGLE_CRUISE_ARGUMENT) {
		toggleCruise();
	}
	if(!gIsCruising) return;

	if(argument == PRECISION_MODE_ARGUMENT) {
		TogglePrecision();
	}
	if (argument == INCREASE_SPEED_ARGUMENT) {
		increaseTargetSpeed();
	}   
	if (argument == DECREASE_SPEED_ARGUMENT) {
		decreaseTargetSpeed();
	}

	UpdateTargetSpeed();
    gCurrentSpeed = (float) getCurrentSpeed(DEF_DECIMAL_PLACES);  
 
    //===ALLIGNING THE CURRENT SPEED TO THE TARGET SPEED=== 
    if (Math.Abs(gCurrentSpeed - gTargetSpeed) > 0.1) { 
        if (gCurrentSpeed < gTargetSpeed && gCurrentAcceleration != 1) { 
            doAccelerate(); 
        } 
        if (gCurrentSpeed > gTargetSpeed && gCurrentAcceleration != -1) { 
            doDecelerate(); 
        } 
    } 
    else if(gCurrentAcceleration != 0) {
    	doCruise();
    }
 
    //===WRITING TO TEXT PANEL===  
    string textToWrite = "CurrentSpeed: " + Math.Round(gCurrentSpeed,1).ToString() + "\n"; 
    textToWrite += "TargetSpeed: " + Math.Round(gTargetSpeed,1).ToString() + "\n";
    if(gPrecisionMode) textToWrite += "Precision Mode...\n";
    gStatusTextPanel.WritePublicText(textToWrite);

} 







double getCurrentSpeed(int decimalPlaces = DEF_DECIMAL_PLACES) {
	if(!gIsCruising) return 0;

    Vector3D currentGlobalGridPosition = Me.GetPosition(); // the position of this programmable block     
 
    if(currentGlobalGridPosition == gGlobalGridPosition) return 0.0; 
         
    double speed = ((currentGlobalGridPosition-gGlobalGridPosition)*60).Length(); // how far the PB has moved since the last run (1/60s ago)             
    gGlobalGridPosition= currentGlobalGridPosition; // update the global variable, which will be used on the next run             
    //speed = Math.Round(speed,decimalPlaces); //rounding the speed
     
    return speed;             
}   

void PowerThrusters(string containingSubstring = "", bool powered = true) {
/*
Powers thrusters if power = true, and unpowers them otherwise. Only operates on thrusters containing the substring containingSubstring
*/
	List <IMyThrust> thrusters = new List<IMyThrust>();
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters, (block) =>
		block.CustomName.Contains("Ion") && block.CustomName.Contains(containingSubstring));
	if(powered) 
		for(int i = 0; i < thrusters.Count; i++) {
			thrusters[i].GetActionWithName("OnOff_On").Apply(thrusters[i]);
		}
	else 
		for(int i = 0; i < thrusters.Count; i++) {
			thrusters[i].GetActionWithName("OnOff_Off").Apply(thrusters[i]);
		}
}
void OverrideThrusters (float overrideValue, string containingSubstring = "") {
/*
Overrides thrusters if doOverride = true, and removes override otherwise. Only operates on thrusters containing the substring containingSubstring
*/
	List <IMyThrust> thrusters = new List<IMyThrust>();
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters, (block) =>
		block.CustomName.Contains("Ion") && block.CustomName.Contains(containingSubstring));
	
	for(int i = 0; i < thrusters.Count; i++) {
		thrusters[i].SetValue("Override", overrideValue); 
		thrusters[i].SetCustomName("[F]" + thrusters[i].CustomName); //debug
	}
}
 
void increaseTargetSpeed() { 
	if(gTargetSpeedChangePerSecond == 0.0F) {	//If the target speed is currently not being changed. In this case, this function should start increasing it
		gTargetSpeedChangePerSecond = TARGET_SPEED_INCREMENT_PER_SECOND;
		if(gPrecisionMode) gTargetSpeedChangePerSecond *= PRECISION_FACTOR;
	}
	else if(gTargetSpeedChangePerSecond > 0.0) {	//If the target speed is currently being changed. In this case, this function should stop increasing it
		gTargetSpeedChangePerSecond = 0.0F;
	}
	
} 
void decreaseTargetSpeed(float speedDecrement = -1.0F) {
	if(gTargetSpeedChangePerSecond == 0.0F) {	//If the target speed is currently not being changed. In this case, this function should start increasing it
		gTargetSpeedChangePerSecond = -TARGET_SPEED_INCREMENT_PER_SECOND;
		if(gPrecisionMode) gTargetSpeedChangePerSecond *= PRECISION_FACTOR;
	}
	else if(gTargetSpeedChangePerSecond < 0.0) {	//If the target speed is currently being changed. In this case, this function should stop increasing it
		gTargetSpeedChangePerSecond = 0.0F;
	}
} 
void UpdateTargetSpeed() {
	gTargetSpeed += gTargetSpeedChangePerSecond / 60;	//TODO: Accomodate for the possibility of simulation speeds different than 60fps

	if(gTargetSpeed > MAX_SPEED) {gTargetSpeed = MAX_SPEED; gTargetSpeedChangePerSecond = 0.0F;}
	else if(gTargetSpeed < 0) {gTargetSpeed = 0; gTargetSpeedChangePerSecond = 0.0F;}
}
void TogglePrecision() {
	gPrecisionMode = !gPrecisionMode;
	if(gPrecisionMode) gTargetSpeedChangePerSecond *= PRECISION_FACTOR;
	else gTargetSpeedChangePerSecond /= PRECISION_FACTOR;
}
void toggleCruise() {
	gIsCruising = !gIsCruising;

	if(gIsCruising) {
		//PowerThrusters("(Backward)",false);
		//PowerThrusters("(Forward)",false);

		gControlTimer.GetActionWithName("OnOff_On").Apply(gControlTimer);
		gControlTimer.GetActionWithName("TriggerNow").Apply(gControlTimer);
	}
	else {
		PowerThrusters();
		OverrideThrusters(0.0F,"(Forward)");

		gControlTimer.GetActionWithName("Stop").Apply(gControlTimer);
		gControlTimer.GetActionWithName("OnOff_Off").Apply(gControlTimer);
		gStatusTextPanel.WritePublicText("Cruise control OFF");
	}
}


void doAccelerate() { 
    List <IMyThrust> forwardThrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(forwardThrusters, (block) => 
        block.CustomName.Contains("Ion") && block.CustomName.Contains("(Forward)")); 
    for (int i = 0; i < forwardThrusters.Count; i++) {
    	forwardThrusters[i].GetActionWithName("OnOff_On").Apply(forwardThrusters[i]);	//Powering on the forward thrusters
        forwardThrusters[i].SetValue("Override", 120.0F); 
    } 

    gStatusTextPanel.WritePublicText("\nAccelerating\n",true);
    gCurrentAcceleration = 1; 
} 
void doDecelerate() { 
/*
Deceleration is going to be done by turning off the forward thrusters and letting the inertia dampeners decelerate the ship
NO USAGE OF BACKWARD THRUSTERS NEEDED
*/
	PowerThrusters("(Backward)");	//Powering backward thrusters up

    List <IMyThrust> forwardThrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(forwardThrusters, (block) => 
        block.CustomName.Contains("Ion") && block.CustomName.Contains("(Forward)"));
    for (int i = 0; i < forwardThrusters.Count; i++) {
        forwardThrusters[i].SetValue("Override", 0.0F); 
    } 

    gStatusTextPanel.WritePublicText("\nDecelerating\n",true);
    gCurrentAcceleration = -1; 
} 
void doCruise() {
/*
Cruise mode simply turns off forward and backward thrusters. Inertia dampeners take care of sideways motion.
If the speed drops too much, the script will call doAccelerate()
*/
	bool thrustersOn = (gTargetSpeed == 0);
	PowerThrusters("(Forward)",thrustersOn);
	PowerThrusters("(Backward)",thrustersOn);

    gStatusTextPanel.WritePublicText("\nCruising\n",true);
    gCurrentAcceleration = 0; 
}