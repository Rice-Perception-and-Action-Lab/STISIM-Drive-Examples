/*************************
' Author:                   Adam Braly
' Example based on code by: Theodore Rosenthal - Systems Technology Inc.
' Date:                     08-27-2018
' Updated:                  11-15-2018
' Language:                 C# COM Interface
' 
' STISIM Open Module Template with examples for
' Auditory warnings
' Tactile warnings
' Visual warnings
' Placing vehicles in the simulation at runtime
***************************/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using Interop.TJRWinTools3; //Don't forget to add TJRWinTools3.dll as a reference
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace Open_Module_Template
{
    [Guid("193C81B4-8633-445B-A571-12AEE7EB3BB3")]
    public interface OMCOM_Interface
    {
        string BSAVData { get; }
        object DashboardForm { set; }
        string ErrorMessage { get; }
        SimEvents EventsIn { set; }
        SimEvents EventsOut { get; }
        int LogFileHandle { set; }
        object NewForm { set; }
        short SaveControls { get; }
        object StartForm { set; }
        string TextMessage { get; }
        int WillHandleCrash { get; }

        //To expose properties and methods to COM, you must declare them on the class interface and mark them with a DispId attribute, and implement them in the class
        [DispId(1)]
        bool StartUp(ref GAINSParams Config, object BackForm, ref OMStaticVariables SV, ref bool UseNew, float[] PlaybackData, string PlaybackString, string ParamFile, TJRSoundEffects SoundIn);
        [DispId(2)]
        bool Initialize(ref OMStaticVariables SV, int[] WorldIndex, TJR3DGraphics GraphicsIn, STI_3D_Terrain TerrainIn);
        [DispId(3)]
        bool AddNew(OMParameters OMVars);
        [DispId(4)]
        bool ControlInputs(DYNAMICSParams Dyn, ref float Steering, ref float Throttle, ref float Brake, ref float Clutch, ref short Gear, ref int DInput);
        [DispId(5)]
        bool Dynamics(ref DYNAMICSParams Dyn);
        [DispId(6)]
        bool HandleCrash(ref short Override, short CrashEvent, short EventIndex);
        [DispId(7)]
        bool Update(ref OMDynamicVariables DV, DYNAMICSParams Vehicle, short NumEvents, ref float[] EDist, short[] EDes, short[] EIndeX);
        [DispId(8)]
        bool Shutdown(int RunCompleted);
        [DispId(9)]
        bool SavePlaybackData(ref float[] PlaybackData, ref string PlaybackString);
        [DispId(10)]
        bool PostRun(string Comments, string DriverName, string RunNumber, string DriverID);

    }

    [Guid("7796F9D9-6DFF-476E-89EC-B89E5E574359"),
        InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface OMCOM_Events
    {

    }

    [Guid("61B32C0D-FFCC-42DD-9095-5FF1F35A4FAB"),
        ClassInterface(ClassInterfaceType.None),
        ComSourceInterfaces(typeof(OMCOM_Events))]
    [ProgId("Open_Module_Template.OM_Module")]
    [Serializable]
    public class OM_Module : OMCOM_Interface
    {
        //add references to STISIM COM objects in TJRWinTools3.dll
        TJR3DGraphics graphicsObj = new TJR3DGraphics();
        TJRSoundEffects soundObj = new TJRSoundEffects();
        STI_3D_Terrain terrainObj = new STI_3D_Terrain();
        TJRWinToolsCls toolsObj = new TJRWinToolsCls();
        

        //worlds used by the graphics object
        static int WORLD_ROADWAY = Convert.ToInt32(SimConstants.WORLD_ROADWAY);
        static int WORLD_SCREEN = Convert.ToInt32(SimConstants.WORLD_ORTHOGRAPHIC);

        //variables used by the current simulation
        static int GRAPHICS_IMAGE_OFF = Convert.ToInt32(GraphicsConstants.GRAPHICS_IMAGE_OFF);
        static int GRAPHICS_IMAGE_ON = Convert.ToInt32(GraphicsConstants.GRAPHICS_IMAGE_ON);
        static int PROJECTION_ORTHOGRAPHIC = Convert.ToInt32(GraphicsConstants.PROJECTION_ORTHOGRAPHIC);
        static int STAGE_NORMAL = Convert.ToInt32(GraphicsConstants.STAGE_NORMAL);
        static int STAGE_ORTHOGONAL = Convert.ToInt32(GraphicsConstants.STAGE_ORTHAGONAL);
        static int TEXTURE_FILTERING_OFF = Convert.ToInt32(GraphicsConstants.TEXTURE_FILTERING_OFF);
        static int LIGHTS_OFF = Convert.ToInt32(SimConstants.LIGHTS_OFF);
        static int LIGHTS_ON = Convert.ToInt32(SimConstants.LIGHTS_ON);
        static int GAPM_PLAY_LOOP = Convert.ToInt32(GraphicsConstants.GAPM_PLAY_LOOP);
        static int DS_PLAY_LOOP = Convert.ToInt32(SoundConstants.DS_PLAY_LOOP);
        static int DS_BSTATUS_PLAYING = Convert.ToInt32(SoundConstants.DS_BSTATUS_PLAYING);

        const float DEG2RAD = 0.0174532f;
        const float WHEELSPINFACTOR = (float)(0.75 * 2 * 3.14159);

        //required for all open module classes even if not used
        public SimEvents Events;
        private string OM_BSAVData;
        private string OM_ErrorMessage;
        private string OM_TextMessage;
        private int OM_LogFileHandle;
        private int OM_WillHandleCrash;
        private short OM_SaveControls;
        private object OM_NewForm;
        private object OM_StartForm;
        private object OM_DashboardForm;

        //tactor vars
        private int ConnectedDeviceID = -1;
        private bool TactorInterfaceInitialized = false;
        private bool TactorControllerConnected = false;
        private const float TACTORINTERVAL = 1.0f;
        static double StartTime;
        static bool TactorsActive = false;
        private string sComName;
        private int TactorToggle;

       


        //structure where we will pass the driver inputs from the SIM
        //using the function ControlInputs
        private struct DriverControlInputs
        {
            public float steeringInput;
            public float throttleInput;
            public float brakeInput;
            public float clutchInput;
            public short gearInput;
            public int buttonInput;
        };

        private DriverControlInputs Driver;


        private struct SoundFiles
        {
            public bool Active;
            public short Buffer;
            public string FileName;
        };

        private SoundFiles[] Sounds = new SoundFiles[1];



        private struct Vehicle
        {
            public int BrakeModel;
            public int Index;
            public float InitialHeading;
            public double Lat;
            public double Lon;
            public SixDOFPosition SixDOF;
            public float Speed;
            public float SpinDuration;
            public int SpinModel;
            public float SpinSpeed;
            public int VisFlag;
        };

        private Vehicle[] V = new Vehicle[2];


        private struct ScreenObjects
        {
            public string Description;
            public int Handle;
            public int ModelID;
            public SixDOFPosition SixDOF;
            public int VisIndex;
        };

        private ScreenObjects[] ScreenObj;


        //define more variables to be used by the class
        private BinaryWriter BinFileData;
        private bool Bool;
        private string BinDataFileName;
        private OMDynamicVariables DynVars = new OMDynamicVariables { };
        private GAINSParams Gains = new GAINSParams { };
        private int ID_Channel;
        private int ID_Screen;
        private int ID_World;
        private int ID_World_New;
        private int NumImages;
        private int NumVehicles;
        private OMStaticVariables StaticVars = new OMStaticVariables { };

        public string BSAVData { get { return OM_BSAVData; } }
        public object DashboardForm { set { OM_DashboardForm = value; } }
        public string ErrorMessage { get { return OM_ErrorMessage; } }
        //public SimEvents EventsIn { set { Events = DeepCopy(value); } }
        public SimEvents EventsIn { set { Events = value; } }
        public SimEvents EventsOut { get { return Events; } }
        public int LogFileHandle { set { OM_LogFileHandle = value; } }
        public object NewForm { set { OM_NewForm = value; } }
        public short SaveControls { get { return OM_SaveControls; } }
        public object StartForm { set { OM_StartForm = value; } }
        public string TextMessage { get { return OM_TextMessage; } }
        public int WillHandleCrash { get { return OM_WillHandleCrash; } }


        public bool StartUp(ref GAINSParams Config, object BackForm, ref OMStaticVariables SV, ref bool UseNew, float[] PlaybackData, string PlaybackString, string ParamFile, TJRSoundEffects SoundIn)
        {
            /*
            Function for handling Open Module processes immediately after the software is started
            '
            '
            '   Parameters:
            '
            '           Config - Configuration file parameters
            '         BackForm - Current STISIM Drive back ground form
            '               SV - User defined type containing simulation static variables
            '           UseNew - Flag specifying if a new background form will be used (True) or not (False)
            '   PlaybackData() - Array containing any data that is being transfered from the playback file back into your module
            '   PlaybackString - String containing any string data that is being transfered from the playback file back into your module
            '        ParamFile - Name of a file that contains any parameters that will be required by the Open Module code
            '          SoundIn - Simulation sound object
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '*/
            //Called after kernel starts and before simulation is initialized. 
            //If you want to create your own custom UI, do it here.

            StreamReader ParamsIn;
            string[] SoundFileNames = new string[3];

            try
            {
                //assign a reference to the local sound object so that it can be used in other modules
                soundObj = SoundIn;

                //if there is an initializaion file specified then can do the following
                if (ParamFile.Length > 0)
                {
                    ParamsIn = new StreamReader(ParamFile);
                    SoundFileNames[0] = ParamsIn.ReadLine();
                    if (!File.Exists(SoundFileNames[0]))
                    {
                        SoundFileNames[0] = null;
                    }
                    SoundFileNames[1] = ParamsIn.ReadLine();
                    if (!File.Exists(SoundFileNames[1]))
                    {
                        SoundFileNames[1] = null;
                    }
                    SoundFileNames[2] = ParamsIn.ReadLine();
                    TactorToggle = Convert.ToInt32(SoundFileNames[2]);
                    ParamsIn.Close();

                }

                //initialize any sounds that will be used
                if (soundObj.SoundEnabled)
                {
                    //Initialize the sound buffer for each auditory object
                    for (int i = 0; i <= Sounds.GetUpperBound(0); i++)
                    {
                        if (SoundFileNames[i] != null)
                        {
                            Sounds[i].FileName = SoundFileNames[i];
                            Sounds[i].Buffer = soundObj.Ds_CreateBuffer(Sounds[i].FileName, false, 0, 0, 0);
                            Sounds[i].Active = true;
                        }
                    }
                }

                //save a local version of the config file
                Gains = Config;

                UseNew = false;
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("StartUp");
                return false;
            }


        }

        public bool Initialize(ref OMStaticVariables SV, int[] WorldIndex, TJR3DGraphics GraphicsIn, STI_3D_Terrain TerrainIn)
        {
            /*'
            '
            '   Function for handling all Open Module initialization
            '
            '
            '   Parameters:
            '
            '             SV - User defined type containing simulation static variables
            '   WorldIndex() - Handle for the various graphics context that hold the roadway environments
            '     GraphicsIn - Reference to the graphics object that the main simulator uses to draw the 3D world
            '      TerrainIn - Reference to the terrain object that is used by the main simulation loop
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '
            '*/
            //Called once before the simulation and is accessed only once during simulation.
            //Do all initializing here.

            if (TactorToggle == 1) //Checks the third line in Open_Module_Template.ini  1 = use tactors 0 = do not use tactors
            {
                //Initialize tactor interface
                try
                {
                    Tdk.TdkInterface.InitializeTI();
                    TactorInterfaceInitialized = true;

                }
                catch
                {
                    TactorInterfaceInitialized = false;
                    //MessageBox.Show(": Unable to initialize tactor interface");
                    OM_ErrorMessage = ProcessError(": Unable to Initialize tactor controller box. Make sure TDKInterface.cs is in your project and make sure the .dll file paths are correct");
                    return false;
                }




                //Conect to tactor conroller
                try
                {
                    int ret = Tdk.TdkInterface.Discover((int)Tdk.TdkDefines.DeviceTypes.Serial);
                    if (ret > 0)
                    {
                        for (int i = 0; i < ret; i++)
                        {
                            //Gets the discovered device name at the index i
                            System.IntPtr discoveredNamePTR = Tdk.TdkInterface.GetDiscoveredDeviceName(i);
                            if (discoveredNamePTR != null)
                            {
                                sComName = Marshal.PtrToStringAnsi(discoveredNamePTR);
                            }
                            else
                            {
                                OM_ErrorMessage = ProcessError(": Unable to connect to tactor controller box. Auto discovery method has failed. Check power and connection to tactor controller.");
                                return false;
                            }

                        }

                    }

                    string selectedComPort = sComName;
                    ret = Tdk.TdkInterface.Connect(selectedComPort,
                                                   (int)Tdk.TdkDefines.DeviceTypes.Serial,
                                                    System.IntPtr.Zero);
                    //MessageBox.Show("ret " + ret);
                    if (ret >= 0)
                    {
                        ConnectedDeviceID = ret;
                        TactorControllerConnected = true;
                    }
                    else
                    {
                        TactorControllerConnected = false;
                        //MessageBox.Show("Unable to connect to tactor controller box");
                        OM_ErrorMessage = ProcessError(": Unable to connect to tactor controller box. Make sure the device is connected to the PC and powered on. Also make sure you are calling the correct COM port");
                        return false;
                    }

                }
                catch
                {
                    TactorControllerConnected = false;
                    //MessageBox.Show("Unable to connect to tactor controller box");
                    OM_ErrorMessage = ProcessError(": Unable to connect to tactor controller box. Unable to call connect function.");
                    return false;
                }
            }
            try
            {
                //Create some variables to be used locally
                ColorAttributes[] BarColors = null;
                float[] BarHeight = null;
                float BarWidth;
                FileStream Fs;
                float HExtent;
                string ImageName;
                int Lng;
                int ModelIndex;
                string[] ModelName = new string[2];
                SixDOFPosition OrthoEye = new SixDOFPosition { };
                int NumVerts;
                float PlaneFar;
                float PlaneNear;
                float[] Speed = new float[2];
                float[] UT = new float[5];
                float VExtent;
                float[] VT = new float[5];
                float[] X = new float[2];
                float[] XPoly = new float[5];
                float[] Y = new float[2];
                float[] YPoly = new float[5];
                float[] ZPoly = new float[5];
                string temp;
                string temp2;


                //get handles to the simulator's 3D roadway and 2D screen world
                ID_World = WorldIndex[WORLD_ROADWAY];
                ID_Screen = WorldIndex[WORLD_SCREEN];


                //create new class constructors here
                graphicsObj = GraphicsIn;
                terrainObj = TerrainIn;



                //Open a binary file so we have a place to put any data that will be collected
                temp = SV.DataLocation.Trim();
                BinDataFileName = temp + "WarningData.Bin";

                if (File.Exists(BinDataFileName))
                {
                    File.Delete(BinDataFileName);
                }
                Fs = new FileStream(BinDataFileName, FileMode.CreateNew);
                BinFileData = new BinaryWriter(Fs);

                //setup labels that will be used to display data in the STISIM Drive runtime window display
                SV.DisplayStrings[1] = "Headway = ";
                SV.DisplayStrings[2] = "Vehicle gap = ";
                SV.DisplayStrings[3] = "Lead vehicle speed = ";


                //create the world for doing anything on the screen
                ID_World_New = graphicsObj.CreateWorld();
                ID_Channel = graphicsObj.AddGraphicsChannel(0, 1, 0, 1, 1, ID_World_New);
                Lng = graphicsObj.ChannelProjectionType(ID_Channel, PROJECTION_ORTHOGRAPHIC);


                //set the eye, FOV, and clipping plane for the overlay graphics channel
                OrthoEye.X = -0.5 * SV.SimWindow.Width / Math.Tan(0.5 * Gains.Displays[0].FieldOfView * DEG2RAD);
                PlaneNear = (float)(-OrthoEye.X - 1);
                PlaneFar = PlaneNear + 2;
                graphicsObj.SetEye(OrthoEye, ID_Channel);
                HExtent = (float)(PlaneNear * Math.Tan(0.5 * Gains.Displays[0].FieldOfView * DEG2RAD));
                VExtent = HExtent * SV.SimWindow.Height / SV.SimWindow.Width;

                graphicsObj.SetFrustrumExtents(ID_Channel, -HExtent, HExtent, VExtent, -VExtent);
                graphicsObj.SetClippingPlanes(ID_Channel, PlaneNear, PlaneFar);

                //Define the number of images and storage arrays
                NumImages = 6;
                Array.Resize(ref ScreenObj, NumImages);
                Array.Resize(ref BarColors, NumImages);
                Array.Resize(ref BarHeight, NumImages);


                //Precompute some values, colors, 62, 63, and 64 must be specified and saved int he STISIM Drive config file
                BarWidth = 0.1f;
                BarHeight[0] = 0.075f;
                BarHeight[1] = BarHeight[0];
                BarHeight[2] = 0.05f;
                BarHeight[3] = BarHeight[2];
                BarHeight[4] = 0.025f;
                BarHeight[5] = BarHeight[4];
                BarColors[5].Green = 0.50f;
                BarColors[4].Green = 1;
                BarColors[3].Green = 0.5f;
                BarColors[3].Red = 0.5f;
                BarColors[2].Green = 1;
                BarColors[2].Red = 1;
                BarColors[1].Red = 0.5f;
                BarColors[0].Red = 1;

                //Define the image with respect to its own internal axis system, the actual placement on the screen
                //will take place next based on the object
                NumVerts = 4;
                for (int i = 0; i <= NumImages - 1; i++)
                {
                    //The warning display consists of 6 bar that individually appear based on how close the driver is
                    //to the lead vehicle, each bar is an individual screen object so that it can be turned on/off
                    //independent of the other display bar, therefore 6 bar are defined
                    temp2 = i.ToString();
                    temp2.Trim();
                    ImageName = "WarningBar" + temp2;
                    ModelIndex = graphicsObj.StartModelDefinition(ImageName, ID_World_New, NumVerts);

                    //set the bar color
                    BarColors[i].Alpha = 1;
                    Lng = graphicsObj.SetMaterial(null, BarColors[i], (short)TEXTURE_FILTERING_OFF, null, null, null);


                    //define bare geometry
                    YPoly[1] = 0;
                    ZPoly[1] = 0;
                    YPoly[2] = YPoly[1];
                    ZPoly[2] = BarHeight[i] * SV.SimWindow.Height;
                    YPoly[3] = BarWidth * SV.SimWindow.Width;
                    ZPoly[3] = ZPoly[2];
                    YPoly[4] = YPoly[3];
                    ZPoly[4] = ZPoly[1];
                    graphicsObj.AddGLPrimitive(NumVerts, XPoly, YPoly, ZPoly, UT, VT, ID_World_New, ModelIndex);
                    Lng = graphicsObj.EndModelDefinition(ID_World_New, ModelIndex);


                    //set each Bar's position on the scren and its scaling
                    ScreenObj[i].SixDOF.Y = SV.SimWindow.OffsetX + 0.2 * SV.SimWindow.Width;
                    if (i == 0)
                    {
                        ScreenObj[i].SixDOF.Z = SV.SimWindow.OffsetY - 0.6 * SV.SimWindow.Height;
                    }
                    else
                    {
                        ScreenObj[i].SixDOF.Z = ScreenObj[i - 1].SixDOF.Z + BarHeight[i - 1] * SV.SimWindow.Height;
                    }
                    ScreenObj[i].Handle = graphicsObj.LoadGraphicObject(ScreenObj[i].SixDOF, ID_World_New, null, ImageName, STAGE_ORTHOGONAL);
                    graphicsObj.SetObjectVisibility(ScreenObj[i].Handle, GRAPHICS_IMAGE_OFF);

                }

                //Assign some properties to the vehicles being added
                ModelName[0] = @"C:\STISIM3\Data\Vehicles\Specialty\NASCAR_Yellow.Mka";
                ModelName[1] = @"C:\STISIM3\Data\Vehicles\Sports Cars\Dodge_Challenger_Green.Mka";
                X[0] = 100;//100
                X[1] = 300;//300
                Y[0] = 8;
                Y[1] = 6;
                Speed[0] = 30;
                Speed[1] = 30;

                //create a couple of vehicle objects
                for (int j = 0; j <= 1; j++)
                {
                    //MessageBox.Show("inside j func" + j);
                    if (File.Exists(ModelName[j]))
                    {
                        // MessageBox.Show("a " + j);
                        SetUpVehicles(NumVehicles, X[j], Y[j], Speed[j], ModelName[j]);
                        //MessageBox.Show("X[0] = " + X[j] + " Y[0] = " + Y[j] + " Speed[0] = " + Speed[j] + " ModelName[0] = " + ModelName[j]);
                        NumVehicles++;
                        //MessageBox.Show("NumVehicles " + NumVehicles);
                    }
                }

                //make the static vars available to all other methods
                StaticVars = SV;

                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("Initialize");
                return false;
            }
        }

        public bool AddNew(OMParameters OMVars)
        {
            /*
            '
            '   Function for adding a new interactive Open Module event
            '
            '
            '   Parameters:
            '
            '   OMVars - User defined type containing the parameters for the given Open Module being acted on
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '*/

            //New user defined events can be initialized and activated during the simulation
            //Current events can be modified by passing new parameter values

            try
            {
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("AddNew");
                return false;
            }

        }

        public bool ControlInputs(DYNAMICSParams Dyn, ref float Steering, ref float Throttle, ref float Brake, ref float Clutch, ref short Gear, ref int DInput)
        {
            /*    
            '
            '   Function for handling any user defined control inputs
            '
            '
            '   Parameters:
            '
            '        Dyn - User defined type containing simulation dynamics variables
            '   Steering - Steering wheel angle input digital count
            '   Throttle - Throttle pedal input digital count
            '      Brake - Braking pedal input digital count
            '     Clutch - Clutch pedal input digital count
            '       Gear - Current transmission gear
            '     DInput - Current button values
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '*/

            //Called each frame of the simulation loop
            //used for modifying driver's default control inputs

            //pass the values from the sim to our local struct Driver
            try
            {
                Driver.steeringInput = Steering;
                Driver.throttleInput = Throttle;
                Driver.brakeInput = Brake;
                Driver.clutchInput = Clutch;
                Driver.gearInput = Gear;
                Driver.buttonInput = DInput;

                Gear = 0;
                //insert new code here for automation levels


                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("ControlInputs");
                return false;
            }
        }

        public bool Dynamics(ref DYNAMICSParams Dyn)
        {
            /*'
            '
            '   Function for handling all Open Module dynamic updates
            '
            '
            '   Parameters:
            '
            '   Dyn - User defined type containing the driver's vehicle dynamic variables
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '
            '*/
            //Modify the dnymamic behavior of the driver's vehicle
            //Can only be accessed when using the simple STISIM drive dynamics

            try
            {
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("Dynamics");
                return false;
            }

        }

        public bool HandleCrash(ref short Override, short CrashEvent, short EventIndex)
        {

            /*'
            '
            '   Function for handling all Open Module action if there is a crash during the simulation run
            '
            '
            '   Parameters:
            '
            '      Override - Parameter defining how STISIM Drive will handle the crash when this
            '                 method returns control to it
            '    CrashEvent - Event designator for the event that caused the crash
            '    EventIndex - Index specifying which instance of the crash event caused the crash
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '*/
            //STISIM is ignorant to OM code that is running. When an accident happens, STISIM calls an
            //accident handling routine that plays a crash sound effect, displays broken glass windshield,
            //and resets the driver. If you don't want this, handle that here.    
            try
            {
                for (int i = 0; i <= NumImages - 1; i++)
                {
                    graphicsObj.SetObjectVisibility(ScreenObj[i].Handle, GRAPHICS_IMAGE_OFF);
                }

                Override = 0;
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("HandleCrash");
                return false;
            }

        }


        public bool Update(ref OMDynamicVariables DV, DYNAMICSParams Vehicle, short NumEvents, ref float[] EDist, short[] EDes, short[] EIndeX)
        {

            /*
            'Function for handling all Open Module action during the actual simulation loop
            '
            '
            '   Parameters:
            '
            '          DV - User defined type containing the simulation parameters that are changing at each time step
            '     Vehicle - User defined type containing the driver's vehicle dynamic variables
            '   NumEvents - Number of events that are in the current display list
            '     EDist() - Distance from the driver to the event
            '      EDes() - Event designator for each active event
            '    EIndex() - Event index for each event in the display list. This value is the index into the Events UDT
            '               so that you can get the parameters for each individual event in the display list
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '*/

            //Called each frame of the simulation loop
            //Run new OM code here

            double CrossSlope = 0;
            double Grade = 0;
            float Headway = 0;
            int i;
            int LaneNum = 0;
            float MinRange;
            float SaveSpeed = 0;
            int SegType = 0;
            float Range = 0;

            try
            {
                //update the vehicle positions
                MinRange = 9999;
                if (DV.Paused == 0)
                {
                    //MessageBox.Show("c: numvehicles" + NumVehicles);
                    for (i = 0; i <= NumVehicles - 1; i++)
                    {
                        // MessageBox.Show("d " + i);
                        V[i].Lon = V[i].Lon + V[i].Speed * DV.TimeInc;
                        //MessageBox.Show("e " + i);
                        terrainObj.RoadQuery(ref V[i].Lon, ref V[i].Lat, ref V[i].SixDOF.X, ref V[i].SixDOF.Y, ref V[i].SixDOF.Z, ref Grade, ref CrossSlope, ref V[i].SixDOF.Psi, ref SegType, ref LaneNum);
                        // MessageBox.Show("f " + i);
                        //MessageBox.Show("Values from RoadQuery during update: V[i].Lon " + V[i].Lon + " V[i].Lat " + V[i].Lat + " V[i].SixDOF.X " + V[i].SixDOF.X + " V[i].SixDOF.Y " + V[i].SixDOF.Y + " V[i].SixDOF.Z " + V[i].SixDOF.Z + " Grade " + Grade + " CrossSlope " + CrossSlope + " V[i].SixDOF.Psi " + V[i].SixDOF.Psi + " SegType " + SegType + " LaneNum " + LaneNum);
                        V[i].SixDOF.Theta = Math.Atan(Grade);
                        // MessageBox.Show("g " + i);
                        V[i].SixDOF.Phi = Math.Atan(CrossSlope);
                        //  MessageBox.Show("h " + i);

                        //update the vehicle's position in the graphics world
                        graphicsObj.SetObjectPosition(ref V[i].Index, ref V[i].SixDOF);
                        //  MessageBox.Show("i " + i);
                        //MessageBox.Show("Values after SetObjPos call in Update: V[i].Index " + V[i].Index + " V[i].SixDOF.X " + V[i].SixDOF.X + " V[i].SixDOF.Y " + V[i].SixDOF.Y + " V[i].SixDOF.Z " + V[i].SixDOF.Z);
                        //change the wheel speed if desired
                        V[i].SpinSpeed = V[i].Speed * V[i].SpinDuration / WHEELSPINFACTOR;
                        //  MessageBox.Show("j " + i);
                        graphicsObj.SetAnimationScale(ref V[i].Index, ref V[i].SpinModel, ref V[i].SpinSpeed);
                        //  MessageBox.Show("k " + i);

                        //compute the values for the closes vehicle that is in the driver's lane
                        //Note: since this is an example, the following only compares the center locations and does not take into
                        //account the true vehicle sizes. Make sure the vehicle is in front of the driver withing the driver's lateral extents

                        if ((V[i].Lon > DV.Distance) && (Math.Abs(DV.LanePosition - V[i].Lat) < 6))
                        {
                            Range = (float)(V[i].Lon - DV.Distance);
                        }
                        else
                        {
                            Range = 9999;
                        }

                        //if this vehicle is closer, save the range
                        if (Range < MinRange)
                        {
                            //  MessageBox.Show("n" + i);
                            MinRange = Range;
                            SaveSpeed = V[i].Speed;
                            //  MessageBox.Show("o" + i);
                        }

                    }
                }


                if (Vehicle.U > 58.67) //speed in feet per second == 40 mph
                {
                    if (TactorsActive)
                    {
                        if ((toolsObj.PrecisionTimer() - StartTime) >= TACTORINTERVAL)
                        {
                            TactorsActive = false;
                        }
                    }
                    else
                    {
                        TactorsActive = true;
                        TactorAlert();
                        StartTime = toolsObj.PrecisionTimer();
                    }
                    graphicsObj.SetObjectVisibility(ScreenObj[5].Handle, GRAPHICS_IMAGE_ON);
                    graphicsObj.SetObjectVisibility(ScreenObj[4].Handle, GRAPHICS_IMAGE_ON);
                    graphicsObj.SetObjectVisibility(ScreenObj[3].Handle, GRAPHICS_IMAGE_ON);
                    graphicsObj.SetObjectVisibility(ScreenObj[2].Handle, GRAPHICS_IMAGE_ON);
                    graphicsObj.SetObjectVisibility(ScreenObj[1].Handle, GRAPHICS_IMAGE_ON);
                    graphicsObj.SetObjectVisibility(ScreenObj[0].Handle, GRAPHICS_IMAGE_ON);
                    AuditoryWarning(true, 0);
                }
                else
                {
                    graphicsObj.SetObjectVisibility(ScreenObj[5].Handle, GRAPHICS_IMAGE_OFF);
                    graphicsObj.SetObjectVisibility(ScreenObj[4].Handle, GRAPHICS_IMAGE_OFF);
                    graphicsObj.SetObjectVisibility(ScreenObj[3].Handle, GRAPHICS_IMAGE_OFF);
                    graphicsObj.SetObjectVisibility(ScreenObj[2].Handle, GRAPHICS_IMAGE_OFF);
                    graphicsObj.SetObjectVisibility(ScreenObj[1].Handle, GRAPHICS_IMAGE_OFF);
                    graphicsObj.SetObjectVisibility(ScreenObj[0].Handle, GRAPHICS_IMAGE_OFF);
                    AuditoryWarning(false, 0);
                }




                // MessageBox.Show("p");
                //compute an approximate headway
                /*if (Vehicle.U > 0) //?? original code ** if Vehicle.U then **
                {
                   // MessageBox.Show("q");
                    Headway = MinRange / Vehicle.U;
                }
                else
                {
                   // MessageBox.Show("r: else backet");
                    Headway = 9999;
                }

                //display the visual warning based on the temporal headway between the driver and the lead vehicle
                if (Headway < 10)
                {
                   // MessageBox.Show("s");
                    graphicsObj.SetObjectVisibility(ScreenObj[5].Handle, GRAPHICS_IMAGE_ON);

                    //turn second green block on
                    if (Headway < 8)
                    {
                       // MessageBox.Show("t");
                        graphicsObj.SetObjectVisibility(ScreenObj[4].Handle, GRAPHICS_IMAGE_ON);

                        //turn first yellow block on
                        if (Headway < 6)
                        {
                          //  MessageBox.Show("u");
                            graphicsObj.SetObjectVisibility(ScreenObj[3].Handle, GRAPHICS_IMAGE_ON);

                            //turn second yellow block on
                            if (Headway < 4)
                            {
                                //MessageBox.Show("v");
                                graphicsObj.SetObjectVisibility(ScreenObj[2].Handle, GRAPHICS_IMAGE_ON);
                                AuditoryWarning(true, 0);

                                //turn first red block on
                                if (Headway < 2)
                                {
                                    //MessageBox.Show("w");
                                    graphicsObj.SetObjectVisibility(ScreenObj[1].Handle, GRAPHICS_IMAGE_ON);

                                    //turn bottom red block and second auditory warning on
                                    if (Headway < 1)
                                    {
                                       // MessageBox.Show("x");
                                        graphicsObj.SetObjectVisibility(ScreenObj[0].Handle, GRAPHICS_IMAGE_ON);
                                       // MessageBox.Show("y");
                                        AuditoryWarning(false, 0);
                                      //  MessageBox.Show("z");
                                        AuditoryWarning(true, 1);
                                    }
                                    else
                                    {
                                        graphicsObj.SetObjectVisibility(ScreenObj[0].Handle, GRAPHICS_IMAGE_OFF);
                                        AuditoryWarning(false, 1);
                                        AuditoryWarning(true, 0);
                                    }
                                }
                                else
                                {
                                    graphicsObj.SetObjectVisibility(ScreenObj[1].Handle, GRAPHICS_IMAGE_OFF);
                                }

                            }
                            else
                            {
                                graphicsObj.SetObjectVisibility(ScreenObj[2].Handle, GRAPHICS_IMAGE_OFF);
                                AuditoryWarning(false, 0);
                            }
                        }
                        else
                        {
                            graphicsObj.SetObjectVisibility(ScreenObj[3].Handle, GRAPHICS_IMAGE_OFF);
                        }
                    }
                    else
                    {
                        graphicsObj.SetObjectVisibility(ScreenObj[4].Handle, GRAPHICS_IMAGE_OFF);
                    }

                }
                else
                {
                    //make sure everything is off
                    AuditoryWarning(false, 0);
                    AuditoryWarning(false, 1);
                    for (i = 0; i <= NumImages - 1; i++)
                    {
                        graphicsObj.SetObjectVisibility(ScreenObj[i].Handle, GRAPHICS_IMAGE_OFF);
                    }
                }
                */
                //update the STISIM Drive runtime window display with our own custom variables
                DV.DisplayStrings[1] = Headway.ToString();
                DV.DisplayStrings[2] = MinRange.ToString();
                DV.DisplayStrings[3] = SaveSpeed.ToString();

                //save the data into a binary file so that it can be looked at later
                BinFileData.Write(DV.TimeSinceStart);
                BinFileData.Write(Headway);
                BinFileData.Write(MinRange);
                BinFileData.Write(SaveSpeed);

                //make the dynamic variables available to all other methods
                DynVars = DV;
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("Update");
                return false;
            }

        }

        public bool Shutdown(int RunCompleted)
        {
            /*
            '
            '   Function for handling Open Module processes immediately after a simulation run has ended
            '
            '
            '   Parameters:
            '
            '   RunCompleted - Flag specifying if the run completed successfully or not
            '
            '                    0 - Aborted before start of run
            '                    1 - Run completed successfully
            '                  > 1 - Aborted during the run
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '
            '
            '*/
            //Called after the simulation is over
            //Do cleanup here

            int i;
            int Ret;

            try
            {
                //shut down any sound objects that were used
                if (soundObj.SoundEnabled)
                {
                    for (i = 0; i <= Sounds.GetUpperBound(0); i++)
                    {
                        if (Sounds[i].Active)
                        {
                            Ret = soundObj.Ds_Stop(Sounds[i].Buffer);
                        }
                    }
                }

                if (BinFileData != null)
                {
                    BinFileData.Close();
                }


                graphicsObj = null;
                terrainObj = null;
                if (TactorControllerConnected)
                {
                    CheckTDKErrors(Tdk.TdkInterface.Close(ConnectedDeviceID));
                }

                if (TactorInterfaceInitialized)
                {
                    CheckTDKErrors(Tdk.TdkInterface.ShutdownTI());
                }
                return true;
            }
            catch
            {
                graphicsObj = null;
                terrainObj = null;
                if (TactorControllerConnected)
                {
                    CheckTDKErrors(Tdk.TdkInterface.Close(ConnectedDeviceID));
                }

                if (TactorInterfaceInitialized)
                {
                    CheckTDKErrors(Tdk.TdkInterface.ShutdownTI());
                }
                OM_ErrorMessage = ProcessError("Shutdown");
                return false;
            }

        }

        public bool SavePlaybackData(ref float[] PlaybackData, ref string PlaybackString)
        {
            /*
            '   Function for specifying any OM data that will be stored as part of a playback file
            '
            '
            '   Parameters:
            '
            '     PlaybackData - Array containing the data that will be saved
            '   PlaybackString - String containing string data that will be saved
            '
            '   Returns:
            '
            '   True if everything worked fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            '*/
            //Called just before PostRun. Store whatever information is necessary into the playback file.
            //When the drive is played back, data stored here is passed back into OM in the StartUp function.
            //StartUp is the only place where this data can be accessed. i.e., first use the PlaybackMode property
            //from the configuration file struct to determine if the run is a playback or not. If it is, access the data.

            try
            {
                return true;
            }
            catch
            {
                OM_ErrorMessage = ProcessError("SavePlaybackData");
                return false;
            }

        }

        public bool PostRun(string Comments, string DriverName, string RunNumber, string DriverID)
        {
            /*Function for handling anything before the software exists
            '
            '
            '   Parameters:
            '
            '     Comments - Comments entered in the subject information form
            '   DriverName - Name of the driver from the subject information form
            '    RunNumber - Run number entered in the subject information form
            '     DriverID - ID entered from the subject information form
            '
            '   Returns:
            '
            '   True if everything initialized fine, otherwise false. If false use the ErrorMessage
            '   parameter to return a message that the program can display to the user
            */
            //Access the simulation right before the kernel is shut down.
            //Ideal for custom results screens!
            BinaryReader BinData;
            string DataSt;
            int FileLength;
            FileStream FsBin;
            FileStream FsText;
            int I;
            int Lng;
            string OutFile;
            int Pos;
            StreamWriter TextData;
            float[] Var = new float[4];
            string temp;

            try
            {
                if (BinDataFileName.Length > 0) //??
                {
                    //open the binary file so we can extract the data and save it as an ASCII text file
                    FsBin = new FileStream(BinDataFileName, FileMode.Open);
                    BinData = new BinaryReader(FsBin);

                    //append the data that was collected to the STISTIM Drive data file so that all 
                    //of the possible data are in a single file
                    temp = StaticVars.DataLocation.ToString();
                    OutFile = temp + StaticVars.DataFileName;
                    FsText = new FileStream(OutFile, FileMode.Open);
                    TextData = new StreamWriter(FsText);

                    //put a header in the file
                    TextData.WriteLine(" ");
                    TextData.WriteLine(" Collision warning system data:");
                    TextData.WriteLine(" ");

                    //loop through the data and save it to the data file
                    Pos = 0;
                    FileLength = (int)FsBin.Length;
                    while (Pos < FileLength)
                    {
                        //get the raw data from the binary file and increment the pointer

                        Var[0] = BinData.ReadSingle();
                        Var[1] = BinData.ReadSingle();
                        Var[2] = BinData.ReadSingle();
                        Var[3] = BinData.ReadSingle();
                        Pos += 16;

                        //save the data to the ASCII output file

                        DataSt = " " + Var[0].ToString("####0.00") + "    " + Var[1].ToString("###0.00") + "     ";
                        DataSt = DataSt + Var[2].ToString("###0.00") + "     " + Var[3].ToString("##0.00");
                        TextData.WriteLine(DataSt);

                    }

                    //close the files
                    BinData.Close();
                    TextData.Close();
                }
                //delete the Binary data file
                File.Delete(BinDataFileName);

                //release some objects that were created
                soundObj = null;
                toolsObj = null;



                return true;
            }
            catch
            {
                //release some objects that were created
                soundObj = null;
                toolsObj = null;

                OM_ErrorMessage = ProcessError("PostRun");
                return false;
            }

        }

        ///////////////////
        //
        // Below are additional methods that are used only for this open module class
        //
        ///////////////////
        private void AuditoryWarning(bool Setting, int Index)
        {
            /*
            '
            '
            '   Routine for turning an auditory warning on or off
            '
            '   Parameters:
            '
            '   Setting - Flag specifying if the warning is to be played (True) or turned off (False)
            '     Index - Index number for the recording being acted upon
            '
            '*/

            int DSBStatus;
            int Ret;

            if (soundObj.SoundEnabled)
            {
                if (Setting)
                {
                    //play the desired sound file
                    if (Sounds[Index].Active)
                    {
                        DSBStatus = soundObj.Ds_GetPlaybackStatus(Sounds[Index].Buffer);
                        if (DSBStatus != DS_BSTATUS_PLAYING)
                        {
                            Ret = soundObj.Ds_Play(Sounds[Index].Buffer, (short)DS_PLAY_LOOP);
                        }
                    }
                }
                else
                {
                    //if a sound is playing, shut it down
                    Ret = soundObj.Ds_Stop(Sounds[Index].Buffer);
                    Ret = soundObj.Ds_SetCurrentPlayPosition(Sounds[Index].Buffer, 0);
                }
            }
        }

        //Replaced by DeepCopy function
        private SimEvents CloneEvents(ref SimEvents StrIn)
        {
            /*Routine for creating a new version of the events structure that can then be changed independent of the original structure
            '   Parameters:
            '
            '   StrIn - Original structure that will be cloned
            '
            '   Returns:
            '
            '   A new copy of the original structure
            */
            return CloneEvents(ref StrIn);

        }

        //Replaced by DeepCopy function
        private OMDynamicVariables CloneStructure(ref OMDynamicVariables StrIn)
        {
            /*Routine for creating a new version of a structure that can then be changed independent of the original structure
            '   Parameters:
            '
            '   StrIn - Original structure that will be cloned
            '
            '   Returns:
            '
            '   A new copy of the original structure
            */
            return CloneStructure(ref StrIn);

        }

        private string ProcessError(string ModuleName)
        {
            /*'   Routine for adding information to the error message that will be returned when the Open Module encounters problems
            '
            '   Parameters:
            '
            '   ModuleName - Name of the method where the error occured
            '
            '   Returns:
            '
            '   New error message including number and description and other information
            '*/

            bool Bool;
            string St;
            string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;


            //build the error message
            St = "Simulation run aborted! An error has occurred in Open Module " + ModuleName + ":" + "\n" + "\n";
            St = St + "Description:  " + errorMessage + "\n";
            Bool = toolsObj.WriteToTJRFile(OM_LogFileHandle, St);
            return St;

        }

        private void SetUpVehicles(int i, float XInit, float YInit, float Speed, string ModelName)
        {
            /*
            '
            '   Routine for setting up vehicles for use during a simulation run
            '
            '
            '   Parameters:
            '
            '       Index - Index of the vehicle being setup
            '       XInit - Initial longitudinal position
            '       YInit - Initial lateral position
            '       Speed - Vehicle speed
            '   ModelName - File name containing the vehicle model being loaded (no brakes)
            '*/
            double CrossSlope = 0;
            double Grade = 0;
            double Heading = 0;
            int LaneNum = 0;
            int ObjHandle = 0;
            int SegType = 0;
            string St = "";
            string temp = "";
            string temp2 = "";

            //Array.Resize(ref V, Index);
            //set vehicles initial terrain position
            V[i].Lon = XInit;
            V[i].Lat = YInit;
            V[i].Speed = Speed;
            V[i].InitialHeading = 0;

            //MessageBox.Show("XInit " + XInit + " YInit " + YInit + " Speed " + Speed + " Name " + ModelName);
            //MessageBox.Show("V.Lon " + V[i].Lon + " V.Lat " + V[i].Lat + " Speed " + V[i].Speed + " Name " + ModelName);
            terrainObj.RoadQuery(ref V[i].Lon, ref V[i].Lat, ref V[i].SixDOF.X, ref V[i].SixDOF.Y, ref V[i].SixDOF.Z, ref Grade, ref CrossSlope, ref Heading, ref SegType, ref LaneNum);
            //MessageBox.Show("Values after RoadQuery call in SetUpVehciles: V[i].Lon " + V[i].Lon + " V[i].Lat " + V[i].Lat + " V[i].SixDOF.X " + V[i].SixDOF.X + " V[i].SixDOF.Y " + V[i].SixDOF.Y + " V[i].SixDOF.Z " + V[i].SixDOF.Z + " Grade " + Grade + " CrossSlope " + CrossSlope + " Heading " + Heading + " SegType " + SegType + " LaneNum " + LaneNum);
            //load image for the vehicle with brake lights off, and set its visibility on
            temp2 = i.ToString();
            temp2 = temp2.Trim();
            St = "Vehicle_" + temp2;    
            V[i].Index = graphicsObj.LoadGraphicObject(V[i].SixDOF, ID_World, ModelName, St, STAGE_NORMAL);
            //MessageBox.Show("Value of Vehicle index after LoadGraphic Object " + V[i].Index);
            V[i].VisFlag = GRAPHICS_IMAGE_ON;
            graphicsObj.SetObjectVisibility(V[i].Index, V[i].VisFlag);

            //get handles for vehicle lights
            V[i].BrakeModel = graphicsObj.GetSubmodelHandle(V[i].Index, "Brake");
            graphicsObj.SetSubmodelColors(V[i].Index, V[i].BrakeModel, LIGHTS_OFF);

            ObjHandle = graphicsObj.GetSubmodelHandle(V[i].Index, "BlinkL");
            graphicsObj.SetSubmodelColors(V[i].Index, ObjHandle, LIGHTS_OFF);

            ObjHandle = graphicsObj.GetSubmodelHandle(V[i].Index, "BlinkR");
            graphicsObj.SetSubmodelColors(V[i].Index, ObjHandle, LIGHTS_OFF);

            ObjHandle = graphicsObj.GetSubmodelHandle(V[i].Index, "HeadLight");
            graphicsObj.SetSubmodelColors(V[i].Index, ObjHandle, LIGHTS_OFF);

            ObjHandle = graphicsObj.GetSubmodelHandle(V[i].Index, "Reverse");
            graphicsObj.SetSubmodelColors(V[i].Index, ObjHandle, LIGHTS_OFF);

            //get the handle for the vehicle wheel spin animation
            V[i].SpinModel = graphicsObj.GetAnimationHandle(V[i].Index, "spin");
            temp = Convert.ToString(V[i].SpinModel);
            V[i].SpinDuration = graphicsObj.GetAnimationHandle(V[i].Index, temp);
            V[i].SpinSpeed = Speed * V[i].SpinDuration / WHEELSPINFACTOR;
            graphicsObj.SetAnimationState(V[i].Index, V[i].SpinModel, GAPM_PLAY_LOOP);
            graphicsObj.SetAnimationScale(V[i].Index, V[i].SpinModel, V[i].SpinSpeed);

        }   

        //Originally thought we would need this function to deep copy TJR interface objects and structs
        //Function didn't work as inteded, didn't use/didn't need
        //Ended up shallow copying structs using the assignment operator
        private static T DeepCopy<T>(T obj)
        {
            BinaryFormatter s = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                s.Serialize(ms, obj);
                ms.Position = 0;
                T t = (T)s.Deserialize(ms);

                return t;

            }
        }

        private bool CheckTDKErrors(int ret)
        {
            if (ret < 0)
            {
                OM_ErrorMessage = ProcessError("There was a TDK Error");
                return false;
            }
            else
            {
                return true;
            }
        }

        //An example function that calls the TDK tactor code
        private void TactorAlert()
        {
            CheckTDKErrors(Tdk.TdkInterface.Pulse(ConnectedDeviceID, 1, 100, 0));
            CheckTDKErrors(Tdk.TdkInterface.Pulse(ConnectedDeviceID, 1, 100, 12));
            CheckTDKErrors(Tdk.TdkInterface.Pulse(ConnectedDeviceID, 1, 100, 24));
            CheckTDKErrors(Tdk.TdkInterface.Pulse(ConnectedDeviceID, 1, 100, 36));
        }

    }
}
