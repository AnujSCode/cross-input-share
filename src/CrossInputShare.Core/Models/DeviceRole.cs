using System;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Defines the role of a device in a session.
    /// Determines what capabilities and permissions the device has.
    /// </summary>
    public enum DeviceRole
    {
        /// <summary>
        /// Server role: Creates sessions, provides keyboard/mouse input,
        /// coordinates connections between clients.
        /// Only one server per session.
        /// </summary>
        Server = 0,
        
        /// <summary>
        /// Client role: Connects to server, receives input from server,
        /// can share screen back to server and other clients.
        /// Multiple clients allowed per session.
        /// </summary>
        Client = 1,
        
        /// <summary>
        /// Auto role: Automatically determines role based on context.
        /// Typically becomes server when creating session, client when joining.
        /// </summary>
        Auto = 2
    }
    
    /// <summary>
    /// Extension methods for DeviceRole enum.
    /// </summary>
    public static class DeviceRoleExtensions
    {
        /// <summary>
        /// Checks if this role can create new sessions.
        /// </summary>
        public static bool CanCreateSession(this DeviceRole role)
        {
            return role == DeviceRole.Server || role == DeviceRole.Auto;
        }
        
        /// <summary>
        /// Checks if this role can provide keyboard/mouse input to other devices.
        /// </summary>
        public static bool CanProvideInput(this DeviceRole role)
        {
            return role == DeviceRole.Server;
        }
        
        /// <summary>
        /// Checks if this role can receive keyboard/mouse input from other devices.
        /// </summary>
        public static bool CanReceiveInput(this DeviceRole role)
        {
            return role == DeviceRole.Client || role == DeviceRole.Auto;
        }
        
        /// <summary>
        /// Checks if this role can share screen to other devices.
        /// All roles can share screen in the any-to-any model.
        /// </summary>
        public static bool CanShareScreen(this DeviceRole role)
        {
            return true; // All roles can share screen
        }
        
        /// <summary>
        /// Checks if this role can receive screens from other devices.
        /// </summary>
        public static bool CanReceiveScreen(this DeviceRole role)
        {
            return true; // All roles can receive screens
        }
        
        /// <summary>
        /// Checks if this role can manage client connections.
        /// </summary>
        public static bool CanManageConnections(this DeviceRole role)
        {
            return role == DeviceRole.Server;
        }
        
        /// <summary>
        /// Gets a display-friendly name for the role.
        /// </summary>
        public static string GetDisplayName(this DeviceRole role)
        {
            return role switch
            {
                DeviceRole.Server => "Server",
                DeviceRole.Client => "Client",
                DeviceRole.Auto => "Auto",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// Gets a description of the role's capabilities.
        /// </summary>
        public static string GetDescription(this DeviceRole role)
        {
            return role switch
            {
                DeviceRole.Server => "Creates sessions, provides keyboard/mouse input, manages connections",
                DeviceRole.Client => "Connects to server, receives input, can share screen",
                DeviceRole.Auto => "Automatically determines role based on context",
                _ => "Unknown role"
            };
        }
        
        /// <summary>
        /// Determines the appropriate role based on session creation context.
        /// </summary>
        /// <param name="isCreatingSession">True if creating a new session, false if joining</param>
        /// <returns>The determined DeviceRole</returns>
        public static DeviceRole DetermineRole(bool isCreatingSession)
        {
            return isCreatingSession ? DeviceRole.Server : DeviceRole.Client;
        }
        
        /// <summary>
        /// Validates if a role transition is allowed.
        /// </summary>
        /// <param name="currentRole">Current role</param>
        /// <param name="newRole">Proposed new role</param>
        /// <returns>True if transition is allowed</returns>
        public static bool IsTransitionAllowed(DeviceRole currentRole, DeviceRole newRole)
        {
            // Server cannot become client (would break session)
            if (currentRole == DeviceRole.Server && newRole == DeviceRole.Client)
                return false;
            
            // Client can become server only if no other server exists
            if (currentRole == DeviceRole.Client && newRole == DeviceRole.Server)
                return true; // Would need additional validation in session
            
            // Auto can transition to any role
            if (currentRole == DeviceRole.Auto)
                return true;
            
            // Same role is always allowed
            if (currentRole == newRole)
                return true;
            
            return false;
        }
    }
}