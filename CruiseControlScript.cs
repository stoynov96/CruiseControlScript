const string INCREASE_SPEED_ARGUMENT = "+speed"; 
const string DECREASE_SPEED_ARGUMENT = "-speed"; 
const string TOGGLE_CRUISE_ARGUMENT = "-toggleCruise";

const string STATUS_PANEL_NAME = "[1] Text panel L";
const string CONTROLLING_TIMER_NAME = "[1] Timer Cruise Control";	//name of the timer that runs this programmable block and triggers itself

const int DEF_DECIMAL_PLACES = 2;
const float DEF_SPEED_INCREMENT = 5.0F;
const float MAX_SPEED = 100.0F; 		//The maximum speed of the world the script is going to be used in
								//(Or the maximum speed the user wants the ship to move with)
const float MAX_SPEED_DEVIATION = 0.5F; //The maximum deviation that the script will allow between the 
                                                                            //target speed and the actual speed 
const string FORWARD_THRUSTERS_TAG = "(Forward)"; 
const string BACKWARD_THRUSTERS_TAG = "(Backward)"; 
 
Vector3D gGlobalGridPosition = new Vector3D(0,0,0); 
float gCurrentSpeed = 0.0F;  
float gTargetSpeed;   //The speed which the ship is supposed to cruise with. 
short gCurrentAcceleration = 0;  // State of acceleration (-1 to decelerate, 1 to accelerate, 0 for neither) 
bool gIsCruising = false;	//true if in cruise mode. False otherwise
IMyTextPanel gStatusTextPanel;
IMyTimerBlock gControlTimer;





public Program() {
    //===GETTING BLOCKS THAT WILL BE USED IN THE SCRIPT===
    gStatusTextPanel = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(STATUS_PANEL_NAME);
    gControlTimer = (IMyTimerBlock) GridTerminalSystem.GetBlockWithName(CONTROLLING_TIMER_NAME);
}
public void Main(string argument) {

	if(argument == TOGGLE_CRUISE_ARGUMENT) {
		toggleCruise();
	}
	if(!gIsCruising) return;

	if (argument == INCREASE_SPEED_ARGUMENT) {
		increaseTargetSpeed();
	}   
	if (argument == DECREASE_SPEED_ARGUMENT) {
		decreaseTargetSpeed();
	}


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
    string textToWrite = "CurrentSpeed: " + gCurrentSpeed.ToString() + "\n"; 
    textToWrite += "TargetSpeed: " + gTargetSpeed + "\n"; 
    gStatusTextPanel.WritePublicText(textToWrite);
} 







double getCurrentSpeed(int decimalPlaces = DEF_DECIMAL_PLACES) {
	if(!gIsCruising) return 0;

    Vector3D currentGlobalGridPosition = Me.GetPosition(); // the position of this programmable block     
 
    if(currentGlobalGridPosition == gGlobalGridPosition) return 0.0; 
         
    double speed = ((currentGlobalGridPosition-gGlobalGridPosition)*60).Length(); // how far the PB has moved since the last run (1/60s ago)             
    gGlobalGridPosition= currentGlobalGridPosition; // update the global variable, which will be used on the next run             
    speed = Math.Round(speed,decimalPlaces); 
     
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
 
void increaseTargetSpeed(float speedIncrement = -1.0F) { 
	if(speedIncrement < 0) {	//If the speed increment passed is less than zero, this function chooses the speed increment
		speedIncrement = (gTargetSpeed / 4) + 1;
	}

    float newSpeed = gTargetSpeed + speedIncrement; 
    if (newSpeed > MAX_SPEED) newSpeed = MAX_SPEED;
	newSpeed = (float)Math.Round(newSpeed,0);
    gTargetSpeed = newSpeed; 
    return; 
} 
void decreaseTargetSpeed(float speedDecrement = -1.0F) {
	if(speedDecrement < 0) {	//If the speed decrement passed is less than zero, this function chooses the speed decrement
		speedDecrement = (gTargetSpeed / 4) + 1;
	}

    float newSpeed = gTargetSpeed - speedDecrement; 
    if (newSpeed < 0) newSpeed = 0;
    newSpeed = (float)Math.Round(newSpeed,0);
    gTargetSpeed = newSpeed;
    return; 
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