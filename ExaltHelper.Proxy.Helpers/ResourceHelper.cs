using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExaltHelper.Proxy.Helpers;

public static class ResourceHelper
{
	public static readonly string Prefix = (Directory.Exists("ExaltHelper_Data") ? "ExaltHelper_Data/" : "MultiTool_Data/");

	private static readonly string[] RequiredFiles = new string[4] { "Objects.xml", "GroundTypes.xml", "version.dll", "version.txt" };

	public static string ObjectsXmlPath => Prefix + "Objects.xml";

	public static string GroundTypesXmlPath => Prefix + "GroundTypes.xml";

	public static string VersionPath => Prefix + "version.dll";

	public static string GameVersionPath => Prefix + "version.txt";

	public static IEnumerable<string> AllResourcesExist()
	{
		return from f in RequiredFiles
			select Prefix + f into path
			where !File.Exists(path)
			select path;
	}

	public static bool CheckResourceVersion()
	{
		return File.Exists(GameVersionPath);
	}
}
