#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Eirora.Editor
{
    /// <summary>
    /// Prepares the Xcode project for sign-in (ADR-0008): the Sign in with Apple capability on the app, and the frameworks
    /// EiroraSignIn.mm and EiroraKeyBackup.mm use on UnityFramework, where Unity compiles the plugins.
    /// </summary>
    public sealed class IosSignInSetup : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string framework = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(framework, "AuthenticationServices.framework", false);
            project.AddFrameworkToProject(framework, "Security.framework", false);
            project.AddFrameworkToProject(framework, "LocalAuthentication.framework", false); // EiroraKeyBackup.mm
            project.WriteToFile(projectPath);

            var capabilities = new ProjectCapabilityManager(projectPath, "Unity-iPhone/Eirora.entitlements", targetGuid: project.GetUnityMainTargetGuid());
            capabilities.AddSignInWithApple();
            capabilities.WriteToFile();

            if (!File.Exists(Path.Combine(report.summary.outputPath, "Unity-iPhone", "Eirora.entitlements")))
            {
                throw new BuildFailedException("The Sign in with Apple entitlement was not written.");
            }
        }
    }
}
#endif
