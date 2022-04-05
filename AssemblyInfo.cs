using System;
using System.Reflection;
using System.Security.Permissions;

[assembly: System.Reflection.AssemblyCompanyAttribute("uGuardian")]
#if DEBUG
#warning DEBUG
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
#else
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
#endif
[assembly: System.Reflection.AssemblyFileVersionAttribute(SMotionLoader.Globals.Version)]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute(SMotionLoader.Globals.Version)]
[assembly: System.Reflection.AssemblyProductAttribute("SMotion-Loader")]
#if BepInEx
[assembly: System.Reflection.AssemblyTitleAttribute("SMotion-Loader-BepInEx")]
#else
[assembly: System.Reflection.AssemblyTitleAttribute("SMotion-Loader")]
#endif
[assembly: System.Reflection.AssemblyVersionAttribute(SMotionLoader.Globals.Version)]
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618