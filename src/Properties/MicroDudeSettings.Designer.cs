﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MicroDude.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    internal sealed partial class MicroDudeSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static MicroDudeSettings defaultInstance = ((MicroDudeSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new MicroDudeSettings())));
        
        public static MicroDudeSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AvrDudePath {
            get {
                return ((string)(this["AvrDudePath"]));
            }
            set {
                this["AvrDudePath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoFlash {
            get {
                return ((bool)(this["AutoFlash"]));
            }
            set {
                this["AutoFlash"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string Programmer {
            get {
                return ((string)(this["Programmer"]));
            }
            set {
                this["Programmer"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Collections.Specialized.StringCollection RecentlyUsedProgrammers {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["RecentlyUsedProgrammers"]));
            }
            set {
                this["RecentlyUsedProgrammers"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AvrdudeConfHash {
            get {
                return ((string)(this["AvrdudeConfHash"]));
            }
            set {
                this["AvrdudeConfHash"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Collections.Specialized.StringCollection Programmers {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["Programmers"]));
            }
            set {
                this["Programmers"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public int OutputDestination {
            get {
                return ((int)(this["OutputDestination"]));
            }
            set {
                this["OutputDestination"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoDetectUsb {
            get {
                return ((bool)(this["AutoDetectUsb"]));
            }
            set {
                this["AutoDetectUsb"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("USB")]
        public string Port {
            get {
                return ((string)(this["Port"]));
            }
            set {
                this["Port"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ColoredOutputEnabled {
            get {
                return ((bool)(this["ColoredOutputEnabled"]));
            }
            set {
                this["ColoredOutputEnabled"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FFDC143C")]
        public string ErrorColor {
            get {
                return ((string)(this["ErrorColor"]));
            }
            set {
                this["ErrorColor"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FFD4AF37")]
        public string WarningColor {
            get {
                return ((string)(this["WarningColor"]));
            }
            set {
                this["WarningColor"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF008000")]
        public string SuccessColor {
            get {
                return ((string)(this["SuccessColor"]));
            }
            set {
                this["SuccessColor"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF808080")]
        public string InfoColor {
            get {
                return ((string)(this["InfoColor"]));
            }
            set {
                this["InfoColor"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF4682B4")]
        public string MicroDudeColor {
            get {
                return ((string)(this["MicroDudeColor"]));
            }
            set {
                this["MicroDudeColor"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool Verbose {
            get {
                return ((bool)(this["Verbose"]));
            }
            set {
                this["Verbose"] = value;
            }
        }
    }
}
