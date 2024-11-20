global using AresFLib;
global using AresILib;
global using AresTLib;
global using AresGlobalMethods;
global using Corlib.NStar;
global using Mpir.NET;
global using System;
global using System.Diagnostics;
global using System.IO;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresFLib.Global;
global using static AresGlobalMethods.DecodingExtents;
global using static AresGlobalMethods.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using String = Corlib.NStar.String;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AresZLib;

public enum UsedMethodsZ
{
	None = 0,
	CompressThenArchive = 1,
	ArchiveThenCompress = 1 << 1,
	ReplaceOtherArchives = 1 << 2,
	DecomposeOtherArchives = 1 << 3,
	ApplyF = 1 << 4,
	ApplyI = 1 << 5,
	ApplyT = 1 << 6,
	ApplyA = 1 << 7,
	ApplyV = 1 << 8,
}

public static class MainClassZ
{
	private static TcpClient? client;
	private static NetworkStream? netStream;
	private static byte[] toSend = [];
	private static Thread thread = new(() => { });
	private static readonly int[] FibonacciSequence = [1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040, 1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986, 102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903];
	private static int fragmentCount;
	private static readonly NList<int> localFragmentCount = [];
	private static readonly bool continue_ = true;
	private static bool isWorking;
	private static readonly object lockObj = new();
	public static UsedMethodsZ PresentMethodsZ { get; set; } = UsedMethodsZ.CompressThenArchive | UsedMethodsZ.ApplyF | UsedMethodsZ.ApplyI;

	public static void Main(string[] args)
	{
#if !RELEASE
		args = ["11000"];
		Thread.Sleep(MillisecondsPerSecond * 7 / 2);
#else
		Thread.Sleep(MillisecondsPerSecond * 3 / 2);
#endif
		if (!(args.Length != 0 && int.TryParse(args[0], out var port) && port >= 1024 && port <= 65535))
			return;
		if (args.Length >= 2 && int.TryParse(args[1], out var mainProcessId))
		{
			var mainProcess = Process.GetProcessById(mainProcessId);
			mainProcess.EnableRaisingEvents = true;
			mainProcess.Exited += (_, _) => Environment.Exit(0);
		}
		Connect(port);
	}

	private static void Connect(int port)
	{
		IPEndPoint ipe = new(IPAddress.Loopback, port); //IP с номером порта
		client = new(); //подключение клиента
		try
		{
			client.Connect(ipe);
			netStream = client.GetStream();
			Thread receiveThread = new(ReceiveData) { IsBackground = true, Name = "Подключение-F" };//получение данных
			receiveThread.Start();//старт потока
		}
		catch
		{
			return;
		}
		while (true)
			SendMessage();
	}

	private static void SendMessage()
	{
		try
		{
			Thread.Sleep(MillisecondsPerSecond / 4);
			if (netStream == null)
				Disconnect();
			else if (toSend.Length != 0)
			{
				var toSendLen = BitConverter.GetBytes(toSend.Length);
				netStream.Write(toSendLen);
				netStream.Write(toSend);
				netStream.Flush(); //удаление данных из потока
				toSend = [];
			}
		}
		catch
		{
			Disconnect();
		}
	}

	private static void ReceiveData()
	{
		var receiveLen = GC.AllocateUninitializedArray<byte>(4);
		byte[] receiveMessage;
		while (true)
		{
			try
			{
				if (netStream != null)
				{
					netStream.ReadExactly(receiveLen);//чтение сообщения
					receiveMessage = GC.AllocateUninitializedArray<byte>(BitConverter.ToInt32(receiveLen));
					netStream.ReadExactly(receiveMessage);
					WorkUpReceiveMessage(receiveMessage);
				}
				else
					Disconnect();
			}
			catch
			{
				Disconnect();
			}
		}
	}

	private static void WorkUpReceiveMessage(byte[] message)
	{
		try
		{
			if (message[0] == 0)
				PresentMethodsZ = (UsedMethodsZ)BitConverter.ToInt32(message.AsSpan(1..));
			else if (message[0] == 1)
			{
				FragmentLength = 1000000 << Min(BitConverter.ToInt32(message.AsSpan(1..)) & 0xF, 11);
				BWTBlockSize = new[] { 50000, 100000, 200000, 500000, 1000000, 2000000, 4000000, 16000000 }[Min((BitConverter.ToInt32(message.AsSpan(1..)) & 0x70) >> 4, 7)];
				LZDictionarySize = new uint[] { 32767, 262143, 1048575, 4194303, 16777215, (1 << 26) - 1, (1 << 28) - 1, 2048000000 }[Min((BitConverter.ToInt32(message.AsSpan(1..)) & 0x380) >> 7, 7)];
			}
			if (message[0] is < 2 or > 4)
				return;
			var filename = Encoding.UTF8.GetString(message[1..]);
			var filename2 = message[0] == 3 ? filename.Split("<>", StringSplitOptions.RemoveEmptyEntries) : default!;
			thread = new((message[0] - 2) switch
			{
				0 => () => MainThread([filename], Path.Combine(Environment.GetEnvironmentVariable("temp") ?? throw new IOException(), Path.GetFileNameWithoutExtension(filename)), Unarchive),
				1 => () => MainThread(filename2, filename2.Length == 1 ? Path.ChangeExtension(filename2[0], ".ares-z") : Path.GetDirectoryName(filename2[0]) + "/" + Path.GetFileName(Path.GetDirectoryName(filename2[0])) + ".ares-z", Archive),
				2 => () => MainThread([filename], Path.GetDirectoryName(filename) + "/" + Path.GetFileNameWithoutExtension(filename), Unarchive),
				3 => () => MainThread([filename], filename, Recompress),
				_ => throw new NotImplementedException(),
			})
			{ IsBackground = true, Name = "Основной процесс" };
			thread.Start();
			Thread thread2 = new(TransferProgress) { IsBackground = true, Name = "Передача прогресса" };
			thread2.Start();
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				SendMessage();
			}
		}
	}

	public static void MainThread(string[] filename, string filename2, Action<string[], string> action, bool send = true)
	{
		try
		{
			Supertotal = 0;
			isWorking = true;
			if (action == Archive)
			{
				localFragmentCount.Replace(filename.Convert(x => (int)((new FileInfo(x).Length + FragmentLength - 1) / FragmentLength)));
				fragmentCount = (int)Min(localFragmentCount.Sum(x => (long)x), int.MaxValue / 10);
			}
			action(filename, filename2);
			lock (lockObj)
			{
				isWorking = false;
				toSend = [1];
				if (send)
					SendMessage();
			}
		}
		catch (DecoderFallbackException)
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [(byte)(action == Archive ? 3 : 2)];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
	}

	private static void TransferProgress()
	{
		Thread.Sleep(MillisecondsPerSecond);
		while (isWorking)
		{
			NList<byte> list =
			[
				0,
				.. BitConverter.GetBytes(Supertotal),
				.. BitConverter.GetBytes(SupertotalMaximum),
				.. BitConverter.GetBytes(Total),
				.. BitConverter.GetBytes(TotalMaximum),
			];
			for (var i = 0; i < ProgressBarGroups; i++)
			{
				list.AddRange(BitConverter.GetBytes(Subtotal[i]));
				list.AddRange(BitConverter.GetBytes(SubtotalMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Current[i]));
				list.AddRange(BitConverter.GetBytes(CurrentMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Status[i]));
				list.AddRange(BitConverter.GetBytes(StatusMaximum[i]));
			}
			lock (lockObj)
				toSend = [.. list];
			Thread.Sleep(MillisecondsPerSecond);
		}
	}

	public static void Disconnect()
	{
		client?.Close();//отключение клиента
		netStream?.Close();//отключение потока
		var tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/Ares-" + Environment.ProcessId + ".tmp";
		if (File.Exists(tempFilename))
			File.Delete(tempFilename);
		Environment.Exit(0); //завершение процесса
	}

	public static void Archive(string[] filename, string filename2)
	{
		Stream? wfs = null;
		var tempFilename = "";
		try
		{
			tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/Ares-" + Environment.ProcessId + ".tmp";
			if (PresentMethodsZ.HasFlag(UsedMethodsZ.CompressThenArchive))
			{
				List<string> tempFiles = [];
				CompressAll(filename, tempFiles);
				filename = [.. tempFiles];
			}
			wfs = File.Open(tempFilename, FileMode.Create);
			wfs.WriteByte((byte)(PresentMethodsZ & (UsedMethodsZ.CompressThenArchive | UsedMethodsZ.ArchiveThenCompress)));
			ArchiveAll(wfs, filename, !PresentMethodsZ.HasFlag(UsedMethodsZ.CompressThenArchive));
			if (PresentMethodsZ.HasFlag(UsedMethodsZ.ArchiveThenCompress))
			{
				wfs.Seek(1, SeekOrigin.Begin);
				var rfs = wfs;
				wfs = File.Open(tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException())
					+ "/Ares-" + Environment.ProcessId + "-0F.tmp", FileMode.Create);
				wfs.WriteByte((byte)(PresentMethodsZ & (UsedMethodsZ.CompressThenArchive | UsedMethodsZ.ArchiveThenCompress)));
				MainClassF.Compress(rfs, wfs);
				rfs.Close();
			}
			wfs.Close();
			File.Move(tempFilename, filename2, true);
		}
		finally
		{
			if (File.Exists(tempFilename))
			{
				wfs?.Close();
				File.Delete(tempFilename);
			}
		}
	}

	public static void ArchiveAll(Stream wfs, string[] filename, bool addHeader = true)
	{
		NList<byte> header = [];
		if (addHeader)
		{
			PreArchive(header, filename);
			wfs.WriteByte(0);
			wfs.Write(BitConverter.GetBytes(header.Length));
			wfs.Write(header.AsSpan());
		}
		for (var i = 0; i < filename.Length; i++)
		{
			var x = filename[i];
			if (File.Exists(x))
				ArchiveExistent(wfs, filename, i, addHeader);
		}
	}

	public static void PreArchive(NList<byte> header, string[] filename)
	{
		static byte[] ToBytes(string x) => Encoding.UTF8.GetBytes(x);
		foreach (var x in filename)
		{
			if (!(header.Length == 0 || header[^1] is (byte)'/' or (byte)'\\'))
				header.Add((byte)'|');
			if (File.Exists(x))
			{
				header.AddRange(ToBytes(Path.GetFileName(x))).Add((byte)':');
				var info = new FileInfo(x);
				header.AddRange(StructAsSpan(new FileInfo2(info.Attributes, info.CreationTimeUtc, info.LastWriteTimeUtc, info.LastAccessTimeUtc, info.UnixFileMode)));
			}
			else if (Directory.Exists(x))
			{
				header.AddRange(ToBytes(Path.GetFileName(x))).Add((byte)'/');
				PreArchive(header, Directory.GetFileSystemEntries(x));
				header.Add((byte)'\\');
			}
		}
	}

	private static void ArchiveExistent(Stream wfs, string[] filename, int i, bool addHeader = true)
	{
		var x = filename[i];
		var info = new FileInfo(x);
		var access = info.LastAccessTimeUtc;
		try
		{
			var rfs = File.OpenRead(x);
			ArchiveOne(rfs, wfs, localFragmentCount[i], addHeader || i != 0, i == filename.Length - 1);
			rfs.Close();
		}
		catch
		{
			throw;
		}
		finally
		{
			info.LastAccessTimeUtc = access;
		}
	}

	private static void CompressAll(string[] filename, List<string> tempFiles)
	{
		tempFiles.Add((Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/Ares-" + Environment.ProcessId + "-header.tmp");
		CompressHeader(filename, tempFiles[^1]);
		for (var i = 0; i < filename.Length; i++)
		{
			var x = filename[i];
			if (File.Exists(x))
				tempFiles.Add(CompressExistent(filename, i));
		}
	}

	private static void CompressHeader(string[] filename, string headerTempFile)
	{
		NList<byte> header = [];
		PreArchive(header, filename);
		var encoded = new ExecutionsF(header).Encode();
		var wfs = File.Open(headerTempFile, FileMode.OpenOrCreate);
		if (encoded.Length < header.Length)
		{
			wfs.Write(BitConverter.GetBytes(encoded.Length));
			wfs.Write(encoded.AsSpan());
		}
		else
		{
			wfs.Write(BitConverter.GetBytes(header.Length));
			wfs.WriteByte(0);
			wfs.Write(header.AsSpan());
		}
		localFragmentCount.Insert(0, (int)GetArrayLength(wfs.Length, FragmentLength));
		wfs.Close();
	}

	private static string CompressExistent(string[] filename, int i)
	{
		var x = filename[i];
		var info = new FileInfo(x);
		var access = info.LastAccessTimeUtc;
		try
		{
			var dic = "FITAV".ToDictionary(x => x, x => (Environment.GetEnvironmentVariable("temp")
				?? throw new IOException()) + "/Ares-" + Environment.ProcessId + "-" + i.ToString() + x + ".tmp");
			using var rfs = File.OpenRead(x);
			var wfs = File.Open(dic['F'], FileMode.OpenOrCreate);
			wfs.WriteByte(64);
			MainClassF.Compress(rfs, wfs);
			wfs.Close();
			try
			{
				MainClassI.Compress(x, dic['I']);
			}
			catch
			{
			}
			rfs.Seek(0, SeekOrigin.Begin);
			wfs = File.Open(dic['T'], FileMode.OpenOrCreate);
			wfs.WriteByte(65);
			try
			{
				MainClassT.Compress(rfs, wfs);
			}
			catch
			{
			}
			wfs.Close();
			return dic.Values.FindMin(y => File.Exists(y) && CreateVar(new FileInfo(y).Length, out var len) > GetArrayLength(rfs.Length, FragmentLength) * 2 + 1 ? len : long.MaxValue) ?? throw new InvalidOperationException();
		}
		catch
		{
			throw;
		}
		finally
		{
			info.LastAccessTimeUtc = access;
		}
	}

	public static void ArchiveOne(Stream rfs, Stream wfs, int fragmentCount, bool firstFragmentSize, bool last)
	{
		var oldPos = rfs.Position;
		fragmentCount = (int)Min((rfs.Length - oldPos + FragmentLength - 1) / FragmentLength, int.MaxValue / 10);
		using NList<byte> bytes = [];
		if (continue_ && fragmentCount != 0)
		{
			Supertotal = 0;
			SupertotalMaximum = fragmentCount * 10;
			var fragmentCount2 = fragmentCount;
			BitList bits = default!;
			int i;
			for (i = FibonacciSequence.Length - 1; i >= 0; i--)
			{
				if (FibonacciSequence[i] <= fragmentCount2)
				{
					bits = new(i + 2, false) { [i] = true, [i + 1] = true };
					fragmentCount2 -= FibonacciSequence[i];
					break;
				}
			}
			for (i--; i >= 0;)
			{
				if (FibonacciSequence[i] <= fragmentCount2)
				{
					bits[i] = true;
					fragmentCount2 -= FibonacciSequence[i];
					i -= 2;
				}
				else
					i--;
			}
			bits.Insert(0, new BitList(6, ProgramVersion));
			var sizeBytes = GC.AllocateUninitializedArray<byte>((bits.Length + 7) / 8);
			bits.CopyTo(sizeBytes, 0);
			wfs.Write(sizeBytes);
		}
		if (fragmentCount > 1)
			bytes.Resize(FragmentLength);
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1)
			{
				var leftLength = (int)(rfs.Length % FragmentLength);
				if (leftLength != 0)
					bytes.Resize(leftLength);
			}
			rfs.ReadExactly(bytes.AsSpan());
			var s = bytes;
			if (firstFragmentSize && !(fragmentCount == 1 && last))
			{
				wfs.Write(s.Length < 16000000 ? [(byte)(s.Length >> (BitsPerByte << 1)),
					unchecked((byte)(s.Length >> BitsPerByte)), unchecked((byte)s.Length)]
					: [255, 255, 255, (byte)(s.Length >> BitsPerByte * 3), unchecked((byte)(s.Length >> (BitsPerByte << 1))),
					unchecked((byte)(s.Length >> BitsPerByte)), unchecked((byte)s.Length)]);
			}
			wfs.Write(s.AsSpan());
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
	}

	public static void Unarchive(string[] filename, string filename2)
	{
		if (Directory.Exists(filename2))
			Directory.EnumerateFiles(filename2).ForEach(x => x.TryWrap(x => File.Delete(x)));
		else
			Directory.CreateDirectory(filename2);
		if (filename == null || filename.Length != 1 || string.IsNullOrEmpty(filename[0]))
			throw new DecoderFallbackException();
		Stream rfs = File.OpenRead(filename[0]);
		if (rfs.Length - rfs.Position < 1)
			throw new DecoderFallbackException();
		var order = (UsedMethodsZ)rfs.ReadByte();
		if (order.HasFlag(UsedMethodsZ.ArchiveThenCompress))
		{
			MemoryStream wfs = new();
			MainClassF.Decompress(rfs, wfs);
			wfs.Seek(0, SeekOrigin.Begin);
			rfs = wfs;
		}
		var tempDir = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/Ares-" + Environment.ProcessId + "-Temp";
		UnarchiveAll(rfs, tempDir, !order.HasFlag(UsedMethodsZ.CompressThenArchive));
		if (order.HasFlag(UsedMethodsZ.CompressThenArchive))
		{
			DecompressAll(tempDir, filename2);
		}
		else
		{
			Directory.EnumerateFiles(tempDir).ForEach(x => x.TryWrap(x => File.Move(x, filename2 + "/" + Path.GetFileName(x))));
			Directory.Delete(tempDir, true);
		}
		rfs.Close();
	}

	private static void UnarchiveAll(Stream rfs, string filename, bool second)
	{
		NList<byte> compressedHeader = [];
		if (rfs == null || rfs.Length - rfs.Position < compressedHeader.Length.GetSize())
			throw new DecoderFallbackException();
		var readByte = (byte)rfs.ReadByte();
		var encodingVersion = (byte)(readByte & 63);
		var lengthBytes = new byte[compressedHeader.Length.GetSize()];
		rfs.ReadExactly(lengthBytes);
		compressedHeader.Resize(BitConverter.ToInt32(lengthBytes));
		var headerVersion = second ? encodingVersion : (byte)rfs.ReadByte();
		if (rfs.Length - rfs.Position < compressedHeader.Length)
			throw new DecoderFallbackException();
		rfs.ReadExactly(compressedHeader.AsSpan());
		var header = headerVersion == 0 ? compressedHeader : new DecodingF().Decode(compressedHeader, headerVersion);
		List<(string Name, FileInfo2 Info)> files = [];
		PreUnarchive(header, files);
		if (Directory.Exists(filename))
			Directory.EnumerateFiles(filename).ForEach(x => x.TryWrap(x => File.Delete(x)));
		else
			Directory.CreateDirectory(filename);
		for (var i = 0; i < files.Length; i++)
		{
			var (Name, Info) = files[i];
			var wfs = File.Open(filename + "/" + Name, FileMode.Create);
			UnarchiveOne(rfs, wfs, i == files.Length - 1);
			wfs.Close();
			var info = new FileInfo(filename + "/" + Name) { Attributes = Info.Attributes, CreationTimeUtc = Info.Created, LastWriteTimeUtc = Info.Modified, LastAccessTimeUtc = Info.Opened };
			if (!OperatingSystem.IsWindows())
				info.UnixFileMode = Info.FileMode;
		}
	}

	private static void PreUnarchive(NList<byte> header, List<(string Name, FileInfo2 Info)> files)
	{
		static string ToString(ReadOnlySpan<byte> x) => Encoding.UTF8.GetString(x);
		var pos = 0;
		do
		{
			if (pos != 0 && header[pos++] != (byte)'|')
				throw new DecoderFallbackException();
			var oldPos = pos;
			while (pos < header.Length && header[pos] != (byte)':')
				pos++;
			if (pos == oldPos)
				throw new DecoderFallbackException();
			var filename = ToString(header.AsSpan(oldPos..pos));
			pos++;
			FileInfo2 fileInfo = new();
			if (pos > header.Length - fileInfo.GetSize())
				throw new DecoderFallbackException();
			fileInfo = header.AsSpan(pos..(pos += fileInfo.GetSize())).AsStruct<FileInfo2>();
			files.Add((filename, fileInfo));
		} while (pos < header.Length);
	}

	private static void DecompressAll(string sourceDir, string targetDir)
	{
		var files = Directory.GetFiles(sourceDir);
		for (var i = 0; i < files.Length; i++)
		{
			var x = files[i];
			if (File.Exists(x))
				DecompressExistent(x, Path.Combine(targetDir, Path.GetFileName(x)));
		}
	}

	private static void DecompressExistent(string source, string target)
	{
		var x = source;
		var info = new FileInfo(x);
		var access = info.LastAccessTimeUtc;
		try
		{
			var rfs = File.OpenRead(source);
			var firstByte = rfs.ReadByte();
			if (firstByte < 64)
				MainClassI.Decompress(x, target);
			else
			{
				var wfs = File.Open(target, FileMode.OpenOrCreate);
				new[] { MainClassF.Decompress, MainClassT.Decompress }[firstByte - 64](rfs, wfs);
				wfs.Close();
			}
			rfs.Close();
		}
		catch
		{
			throw;
		}
		finally
		{
			if (File.Exists(target))
				new FileInfo(target).LastAccessTimeUtc = access;
		}
	}

	private static void UnarchiveOne(Stream rfs, Stream wfs, bool last)
	{
		PreservedFragmentLength = FragmentLength;
		FragmentLength = 2048000000;
		var readByte = (byte)rfs.ReadByte();
		var encodingVersion = (byte)(readByte & 63);
		if (encodingVersion == 0)
		{
			var bytes2 = rfs.Length < 2048000000 ? default! : GC.AllocateUninitializedArray<byte>(2048000000);
			for (var i = 1; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = GC.AllocateUninitializedArray<byte>(length);
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
			return;
		}
		if (continue_)
		{
			fragmentCount = 0;
			DecodeFibonacci(rfs, readByte);
			SupertotalMaximum = fragmentCount * 10;
		}
		using NList<byte> bytes = [], sizeBytes = RedStarLinq.NEmptyList<byte>(4);
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1 && last)
			{
				bytes.Resize((int)Min(rfs.Length - rfs.Position, 2048000002));
				rfs.ReadExactly(bytes.AsSpan());
			}
			else
			{
				rfs.ReadExactly(sizeBytes.AsSpan(0, 3));
				var fragmentLength = Min(sizeBytes[0] * ValuesIn2Bytes + sizeBytes[1] * ValuesInByte + sizeBytes[2], 2048000002);
				if (fragmentLength > 16000010)
				{
					rfs.ReadExactly(sizeBytes.AsSpan());
					fragmentLength = Min(sizeBytes[0] * ValuesIn3Bytes + sizeBytes[1] * ValuesIn2Bytes + sizeBytes[2] * ValuesInByte + sizeBytes[3], 2048000002);
				}
				bytes.Resize(fragmentLength);
				rfs.ReadExactly(bytes.AsSpan());
			}
			var s = bytes;
			wfs.Write(s.AsSpan());
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
		FragmentLength = PreservedFragmentLength;
	}

	private static void Recompress(string[] filename, string filename2)
	{
		MemoryStream rfs = new(), wfs = new();
		PreservedFragmentLength = FragmentLength;
		FragmentLength = 2048000000;
		var readByte = (byte)rfs.ReadByte();
		var encodingVersion = (byte)(readByte & 63);
		if (encodingVersion >= ProgramVersion)
			wfs.WriteByte(readByte);
#pragma warning disable CS8794 // Входные данные всегда соответствуют предоставленному шаблону.
		if (encodingVersion is 0 or >= ProgramVersion)
#pragma warning restore CS8794 // Входные данные всегда соответствуют предоставленному шаблону.
		{
			var bytes2 = rfs.Length < 2048000000 ? default! : GC.AllocateUninitializedArray<byte>(2048000000);
			for (var i = 1; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = GC.AllocateUninitializedArray<byte>(length);
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
			return;
		}
		if (continue_)
		{
			fragmentCount = 0;
			DecodeFibonacci(rfs, readByte);
			SupertotalMaximum = fragmentCount * 10;
		}
		using NList<byte> bytes = [], sizeBytes = RedStarLinq.NEmptyList<byte>(4);
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1)
			{
				bytes.Resize((int)Min(rfs.Length - rfs.Position, 2048000002));
				rfs.ReadExactly(bytes.AsSpan());
			}
			else
			{
				rfs.ReadExactly(sizeBytes.AsSpan(0, 3));
				var fragmentLength = Min(sizeBytes[0] * ValuesIn2Bytes + sizeBytes[1] * ValuesInByte + sizeBytes[2], 2048000002);
				if (fragmentLength > 16000010)
				{
					rfs.ReadExactly(sizeBytes.AsSpan());
					fragmentLength = Min(sizeBytes[0] * ValuesIn3Bytes + sizeBytes[1] * ValuesIn2Bytes + sizeBytes[2] * ValuesInByte + sizeBytes[3], 2048000002);
				}
				bytes.Resize(fragmentLength);
				rfs.ReadExactly(bytes.AsSpan());
			}
			var s = new ExecutionsF(new DecodingF().Decode(bytes, encodingVersion)).Encode();
			if (fragmentCount != 1)
				wfs.Write([(byte)(s.Length >> (BitsPerByte << 1)), unchecked((byte)(s.Length >> BitsPerByte)), unchecked((byte)s.Length)]);
			wfs.Write(s.AsSpan());
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
		if (wfs.Position > rfs.Length)
		{
			wfs.Seek(0, SeekOrigin.Begin);
			rfs.Seek(0, SeekOrigin.Begin);
			var bytes2 = rfs.Length < 2048000000 ? default! : GC.AllocateUninitializedArray<byte>(2048000000);
			for (var i = 0; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = GC.AllocateUninitializedArray<byte>(length);
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
		}
		FragmentLength = PreservedFragmentLength;
	}

	private static void DecodeFibonacci(Stream rfs, byte readByte)
	{
		BitList bits;
		bool one = false, success = false;
		var sequencePos = 0;
		while (1 == 1)
		{
			if (sequencePos >= 2)
				readByte = (byte)rfs.ReadByte();
			bits = sequencePos < 2 ? new(2, (byte)(readByte >> 6)) : new(8, readByte);
			for (var i = 0; i < bits.Length; i++)
			{
				if (bits[i] && one || sequencePos == FibonacciSequence.Length)
				{
					success = true;
					break;
				}
				else
				{
					if (bits[i]) fragmentCount += FibonacciSequence[sequencePos];
					sequencePos++;
					one = bits[i];
				}
			}
			if (success)
				break;
		}
	}
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct FileInfo2(FileAttributes Attributes, DateTime Created, DateTime Modified, DateTime Opened, UnixFileMode FileMode);
