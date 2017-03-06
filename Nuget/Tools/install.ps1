param($installPath, $toolsPath, $package, $project)

$buildProject = Get-MSBuildProject $project.ProjectName
$target = $buildProject.Xml.AddTarget("RAspectAfterbuild")
$target.AfterTargets = "AfterBuild"
$task = $target.AddTask("Exec")
$task.SetParameter("Command", "`"`$(SolutionDir)packages\RAspect.1.0.0.0\tools\RAspect.compiler.exe`" `"`$(TargetPath)`"")
$project.Save() #persists the changes