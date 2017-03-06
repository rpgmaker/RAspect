param($installPath, $toolsPath, $package, $project)

$buildProject = Get-MSBuildProject $project.ProjectName
$projectRoot = $buildProject.Xml;
Foreach ($target in $projectRoot.Targets)
{
If ($target.Name -eq "RAspectAfterbuild")
{
$projectRoot.RemoveChild($target);
}
}
$project.Save() #persists the changes