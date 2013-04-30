Ialdabaoth's Modular Fuel Tank system
source at: https://github.com/Ialdabaoth/modularFuelTanks

Released through GPL3.0 (http://www.gnu.org/licenses/gpl.html)


HOW IT WORKS:

First, you need a part configured with a moduleFuelTanks, like so:


MODULE
{
	name = moduleFuelTanks
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