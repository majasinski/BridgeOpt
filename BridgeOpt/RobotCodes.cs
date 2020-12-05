using System;

using RobotOM;

namespace BridgeOpt
{
    public class RobotCodes
    {
        public IRobotApplication Application
        {
            get; private set;
        }

        public void InitializeRobot()
        {
            Application = new RobotApplication();
            Application.Interactive = 0;
        }
        
        public void DisposeRobot()
        {
            Application.Quit(IRobotQuitOption.I_QO_DISCARD_CHANGES);
            Application = null;
        }
    }
}