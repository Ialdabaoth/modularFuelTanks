Ialdabaoth's Modular Fuel Tank system
source at: https://github.com/Ialdabaoth/modularFuelTanks

Released through GPL3.0 (http://www.gnu.org/licenses/gpl.html)


HOW IT WORKS:

1. First, you need a part configured with a ModuleFuelTanks, like so:


MODULE
{
	name = ModuleFuelTanks
	volume = 400
	basemass = 0.25
	TANK
	{
	   name = LiquidFuel
	   efficiency = 1.0
	   mass = 0.00
	} 
	TANK
	{
	   name = Oxidizer
	   efficiency = 1.0
	   mass = 0.00
	} 
	TANK
	{
	   name = LiquidH2
	   efficiency = 0.875
	   mass = 0.0025
	   loss_rate = 0.000025
	} 
	TANK
	{
	   name = LiquidOxygen
	   efficiency = 0.975
	   mass = 0.001
	} 
	TANK
	{
	   name = MonoPropellant
	   efficiency = 1.0
	   mass = 0.0005
	} 
}

RESOURCE
{
 name = LiquidFuel
 amount = 180
 maxAmount = 180
}

RESOURCE
{
 name = Oxidizer
 amount = 220
 maxAmount = 220
}


This tells the module that this tank can hold LiquidFuel, Oxidizer, MonoPropellant, LiquidH2 and LiquidOxygen. It also tells the module that LiquidOxygen, LiquidHydrogen, and MonoPropellant require additional mass or volume, and that LiquidHydrogen is slightly leaky.


2. Once you have the module specified correctly in part.cfg, you can load up the tank in the editor. After attaching it to your ship, go to the Action Groups menu and click on the tank. There you will see various options - each fuel type will have a button to add a tank, or text boxes to adjust the tank volume and initial amount of that fuel, as well as a button to remove all tanks from this part and a button to clone this part to all corresponding symmetrical parts. If the system detects an engine attached, it will (usually) also give you a button to remove all tanks and then fill the entire volume with that engine's fuel, in the ratio that that engine consumes it.