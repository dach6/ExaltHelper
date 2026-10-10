using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using ExaltHelper.Proxy.Networking;

namespace ExaltHelper;

internal class IpcPipe
{
	public const string MultiToolPipeName = "MultiToolIpPipe";

	public static void StartServer(MainForm mainForm)
	{
		Task.Run(async delegate
		{
			PipeSecurity pipeSecurity = new PipeSecurity();
			pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
			while (true)
			{
				try
				{
					using NamedPipeServerStream namedPipeServerStream = new NamedPipeServerStream("MultiToolIpPipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
					await namedPipeServerStream.WaitForConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
					using StreamReader streamReader = new StreamReader(namedPipeServerStream);
					ClientsHelper.ConnectServer(await streamReader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false));
				}
				catch (Exception exception)
				{
					Program.ShowError($"Failed reading from named pipe:\n{exception}");
				}
			}
		});
	}

	public static void SendIpToInstance(string serverIp)
	{
		try
		{
			using NamedPipeClientStream namedPipeClientStream = new NamedPipeClientStream(".", "MultiToolIpPipe", PipeDirection.Out);
			namedPipeClientStream.Connect(2000);
			using StreamWriter streamWriter = new StreamWriter(namedPipeClientStream)
			{
				AutoFlush = true
			};
			streamWriter.WriteLine(serverIp);
		}
		catch (Exception exception)
		{
			Program.ShowError($"Failed writing to named pipe:\n{exception}");
		}
	}
}
