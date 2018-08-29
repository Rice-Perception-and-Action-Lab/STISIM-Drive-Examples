//Adam Braly
//1/27/2018

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using TJRWinTools; //Don't forget to add TJRWinTools.dll as a reference

namespace Open_Module_Template
{
    public class OM_Module
    {
        //add references to STISIM COM objects in TJRWinTools3.dll
        TJR3DGraphics graphicsObj = new TJR3DGraphics();
        TJRSoundEffects soundObj = new TJRSoundEffects();
        STI_3D_Terrain terrainObj = new STI_3D_Terrain();
        TJRWinToolsCls toolsObj = new TJRWinToolsCls();

        //required in open module even if not used
        string OM_BSAVData;
        string OM_ErrorMessage;
        string OM_TextMessage;
        int OM_LogFileHandle; 
        int OM_WillHandleCrash;
        short OM_SaveControls;
        object OM_NewForm;
        object OM_StartForm;      
        object OM_DashboardForm;

        
        //structure where we will pass the driver inputs from the SIM
        //using the function ControlInputs
        struct DriverControlInputs 
        {
            public double steeringInput;
            public double throttleInput;
            public double brakeInput;
            public double clutchInput;
            public short gearInput;
            public int buttonInput;     
        };

        DriverControlInputs Driver;

        OMDynamicVariables DynVars;
        OMStaticVariables StaticVars;
        int[] WorldIndex;

        public bool StartUp(GAINSParams Config, object BackForm, OMStaticVariables SV, bool UseNew, double[] PlaybackData, string PlaybackString, string ParamFile, TJRSoundEffects Sound) 
        {
            //Called after kernel starts and before simulation is initialized. 
            //If you want to create your own custom UI, do it here.

            soundObj = Sound; //check wheter this shouldn't be in the Initialize function
            return true;
        }
   
        public bool Initialize(OMStaticVariables SV, int[] WorldIndexIn, TJR3DGraphics GraphicsIn, STI_3D_Terrain TerrainIn)
        {
            //Called once before the simulation and is accessed only once during simulation.
            //Do all initializing here.

            //create new class constructors here
            //e.g., for the traffic class

            graphicsObj = GraphicsIn;
            terrainObj = TerrainIn;
            StaticVars = SV;

            int worldNum;
            worldNum = WorldIndexIn.GetUpperBound(0); //check to make sure this works
            for (int i = 0; i <= worldNum; i++)
            {
                WorldIndex[i] = WorldIndexIn[i]; 
            }           
 

            return true;
        }

        public bool AddNew(OMParameters OMVars)
        {
            //New user defined events can be initialized and activated during the simulation
            //Current events can be modified by passing new parameter values

            return true;
        }

        public bool ControlInputs(DYNAMICSParams Dyn, double Steering, double Throttle, double Brake, double Clutch, short Gear, int DInput) 
        {
            //Called each frame of the simulation loop
            //used for modifying driver's default control inputs

            //pass the values from the sim to our local struct Driver
            Driver.steeringInput = Steering;
            Driver.throttleInput = Throttle;
            Driver.brakeInput = Brake;
            Driver.clutchInput = Clutch;
            Driver.gearInput = Gear;
            Driver.buttonInput = DInput;


            //insert new code here for automation levels

            
            return true;
        }

        public bool Dynamics(DYNAMICSParams Dyn)
        {
            //Modify the dnymamic behavior of the driver's vehicle
            //Can only be accessed when using the simple STISIM drive dynamics

            return true;
        }

        public bool HandleCrash(int Override, SimEvents Events, int CrashEvent, int EventIndex) 
        {
            //STISIM is ignorant to OM code that is running. When an accident happens, STISIM calls an
            //accident handling routine that plays a crash sound effect, displays broken glass windshield,
            //and resets the driver. If you don't want this, handle that here.    
        
            return true;        
        }


        public bool Update(OMDynamicVariables DV, DYNAMICSParams Vehicle, SimEvents Events, int NumEvents, double[] EDist, int[] EDes, int[] EIndeX)
        {
            //Called each frame of the simulation loop
            //Run new OM code here

            return true;
        }
        
        public bool Shutdown(int RunCompleted) 
        {
            //Called after the simulation is over
            //Do cleanup here

            GC.Collect();         
                  
            return true;
        }
   
        public bool SavePlaybackData(double[] PlaybackData, string PlaybackString)
        {
            //Called just before PostRun. Store whatever information is necessary into the playback file.
            //When the drive is played back, data stored here is passed back into OM in the StartUp function.
            //StartUp is the only place where this data can be accessed. i.e., first use the PlaybackMode property
            //from the configuration file struct to determine if the run is a playback or not. If it is, access the data.

            return true;
        }

       public bool PostRun(string Comments, string DriverName, string RunNumber, string DriverID) 
        {
            //Access the simulation right before the kernel is shut down.
            //Ideal for custom results screens!


            return true;
        }

    }
}
