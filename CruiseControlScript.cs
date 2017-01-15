const string INCREASE_SPEED_ARGUMENT = "+speed"; 
const string DECREASE_SPEED_ARGUMENT = "-speed"; 
const string TOGGLE_CRUISE_ARGUMENT = "-toggleCruise";
const string PRECISION_MODE_ARGUMENT = "-precisionMode";
const string POLARIZE_SPEED_ARGUMENT = "-polarizeSpeed";	//This argument goes to either pole (0 or max speed),
									//depending on which one the current target speed is farthest away from

const string CONTROL_BLOCK_NAME = "[1] Main Cockpit";
const string DEBUG_PANEL_NAME = "[DEBUG] Text Panel";
const string STATUS_PANEL_NAME = "[1] Text panel L";
const string CONTROLLING_TIMER_NAME = "[1] Timer Cruise Control";	//name of the timer that runs this programmable block and triggers itself

const short DEF_DECIMAL_PLACES = 2;
const float TARGET_SPEED_INCREMENT_PER_SECOND = 20.0F;	//The rate at which target speed is changed
const float MAX_SPEED = 100.0F; 	//DO NOT SET TO 0! The maximum speed of the world the script is going to be used in
								//(Or the maximum speed the user wants the ship to move with)
const float MAX_SPEED_DEVIATION = 0.2F; //The maximum deviation that the script will allow between the 
                                                                            //target speed and the actual speed 
const float PRECISION_FACTOR = 0.2F;	//DO NOT SET TO ZERO
				//The precision factor determines how much slower speed control is under precision mode. 0.1 means 10 times slower
const string FORWARD_THRUSTERS_TAG = "(Forward)"; 
const string BACKWARD_THRUSTERS_TAG = "(Backward)"; 

//===COLORS===
Color Violet = new Color (190,200,255);
Color LightBlue = new Color (200,255,255);

 
Vector3D gGlobalGridPosition = new Vector3D(0,0,0); 
float gCurrentSpeed = 0.0F;  
float gTargetSpeed;   //The speed which the ship is supposed to cruise with. 
float gTargetSpeedChangePerSecond = 0.0F;	//Current rate of change of the target speed per second
short gCurrentAcceleration = 0;  // State of acceleration (-1 to decelerate, 1 to accelerate, 0 for neither) 
bool gIsCruising = true;	//true if in cruise mode. False otherwise
bool gPrecisionMode = false;	//true if precision mode is on. Precision mode allows for a precise speed control
IMyTextPanel gStatusTextPanel;
IMyTimerBlock gControlTimer;
IMyShipController gMainControllerBlock;

//Debug variables
IMyTextPanel gDebugTextPanel;





public Program() {
    //===GETTING BLOCKS THAT WILL BE USED IN THE SCRIPT===
    gStatusTextPanel = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(STATUS_PANEL_NAME);
    gControlTimer = (IMyTimerBlock) GridTerminalSystem.GetBlockWithName(CONTROLLING_TIMER_NAME);

    gMainControllerBlock = (IMyShipController) GridTerminalSystem.GetBlockWithName(CONTROL_BLOCK_NAME);

    gDebugTextPanel = (IMyTextPanel) GridTerminalSystem.GetBlockWithName(DEBUG_PANEL_NAME);
}
public void Main(string argument) {

	if(argument == TOGGLE_CRUISE_ARGUMENT) {
		toggleCruise();
	}
	if(!gIsCruising) return;

	if(argument == POLARIZE_SPEED_ARGUMENT) {
		PolarizeSpeed();
	}
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
    if(gCurrentSpeed > MAX_SPEED) gCurrentSpeed = MAX_SPEED;
    if(gTargetSpeed > MAX_SPEED) gTargetSpeed = MAX_SPEED;
 
    //===ALLIGNING THE CURRENT SPEED TO THE TARGET SPEED===
    if( Math.Abs(gTargetSpeed - gCurrentSpeed) > MAX_SPEED_DEVIATION) {
    	if ( (gTargetSpeed - gCurrentSpeed > MAX_SPEED_DEVIATION) && gCurrentAcceleration != 1) { 
			doAccelerate(); 
		} 
		else if ( (gCurrentSpeed - gTargetSpeed > (MAX_SPEED_DEVIATION * 2) ) && gCurrentAcceleration != -1) { 
			doDecelerate(); 
		}
	}
    else if(gCurrentAcceleration != 0) {
    	doCruise();
    }
 
    //===WRITING TO TEXT PANEL===  
	gStatusTextPanel.WritePublicText(gCurrentSpeed.ToString("00.0") + " / " + gTargetSpeed.ToString("00.0") + " (m/s)\n");
	gStatusTextPanel.WritePublicText(GUI_GetSpeedBar(20) + "\n", true);
	if(gPrecisionMode) gStatusTextPanel.WritePublicText("Precision Mode...\n", true);
}







double getCurrentSpeed(int decimalPlaces = DEF_DECIMAL_PLACES) {
	if(!gIsCruising) return 0;

	return gMainControllerBlock.GetShipSpeed();
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

void PolarizeSpeed() {
	if(MAX_SPEED > 2.0F * gTargetSpeed) {	//If the target speed is currently closer to 0 than to MAX_SPEED
		gTargetSpeed = MAX_SPEED;
	}
	else gTargetSpeed = 0.0F;
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
	if(gPrecisionMode) {
		gTargetSpeedChangePerSecond *= PRECISION_FACTOR;
		gStatusTextPanel.SetValue("FontColor", Violet);	//Changing the color of the LCD panel text to indicate precision mode
	}
	else {
		gTargetSpeedChangePerSecond /= PRECISION_FACTOR;
		gStatusTextPanel.SetValue("FontColor", LightBlue);	//Changing the color of the LCD panel text to indicate normal mode
	}
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

string GUI_GetSpeedBar(int barLength) {
	const string EMPTY_FIELD = " ";
	const string TARGET_SPEED_CHAR = "|";
	const string CURRENT_SPEED_CHAR = "-";

	string speedBar = "[";

	float targetSpeedCoefficient;	//The target speed as a percentage of the max speed
	float currentSpeedCoefficient;	//The current speed as a percentage of the max speed
	targetSpeedCoefficient = gTargetSpeed / MAX_SPEED;
	currentSpeedCoefficient = gCurrentSpeed / MAX_SPEED;

	int targetSpeedPosition;	//The position of the bar marking the target speed
	int currentSpeedLength;		//The length of the line marking the current speed
	targetSpeedPosition = (int) Math.Round(barLength * targetSpeedCoefficient, 0);
	currentSpeedLength = (int) Math.Round(barLength * currentSpeedCoefficient, 0);

	if(targetSpeedPosition > currentSpeedLength) {
		for(int i = 0; i < currentSpeedLength; i++)
			speedBar += CURRENT_SPEED_CHAR;
		for(int i = currentSpeedLength; i < targetSpeedPosition; i++)
			speedBar += EMPTY_FIELD;
		speedBar += TARGET_SPEED_CHAR;
		for(int i = targetSpeedPosition; i < barLength; i++)
			 speedBar += EMPTY_FIELD;
	}
	else if(targetSpeedPosition == currentSpeedLength) {
		for(int i = 0; i < currentSpeedLength; i++)
			speedBar += CURRENT_SPEED_CHAR;
		speedBar += TARGET_SPEED_CHAR;
		for(int i = targetSpeedPosition; i < barLength; i++)
			 speedBar += EMPTY_FIELD;
	}
	else if(targetSpeedPosition < currentSpeedLength) {
		for(int i = 0; i < targetSpeedPosition; i++)
			speedBar += CURRENT_SPEED_CHAR;
		speedBar += TARGET_SPEED_CHAR;
		for(int i = targetSpeedPosition; i < currentSpeedLength; i++)
			speedBar += CURRENT_SPEED_CHAR;
		for(int i = currentSpeedLength; i < barLength; i++)
			 speedBar += EMPTY_FIELD;
	}

	speedBar += "]";

	return speedBar;
}