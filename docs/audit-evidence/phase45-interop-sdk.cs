using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace ZwSoft.ZwCAD.Runtime;

public sealed class Interop
{
	private Interop()
	{
	}

	[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
	public static void AttachUnmanagedObject(DisposableWrapper obj, IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
	{
		obj.Attach(unmanagedPointer, autoDelete);
	}

	internal static void Check([MarshalAs(UnmanagedType.U1)] bool returnValue)
	{
		CheckBool(returnValue);
	}

	public static void Check(int returnValue)
	{
		if (returnValue != 0)
		{
			ThrowExceptionForErrorStatus(returnValue);
		}
	}

	public static void CheckCPPErrName(int returnValue)
	{
		if (returnValue != 0)
		{
			throw new Exception((ErrorStatus)returnValue, $"e{(ErrorStatus)returnValue}");
		}
	}

	public static void CheckAds(int returnValue)
	{
		CheckZds(returnValue);
	}

	public static void CheckZds(int returnValue)
	{
		if (returnValue != 5100)
		{
			ThrowExceptionForErrorStatus(3);
		}
	}

	[return: MarshalAs(UnmanagedType.U1)]
	public static bool CheckAdsForCancel(int returnValue)
	{
		switch (returnValue)
		{
		case -5002:
			return false;
		default:
			ThrowExceptionForErrorStatus(3);
			break;
		case 5000:
		case 5100:
			break;
		}
		return true;
	}

	public static void CheckBool([MarshalAs(UnmanagedType.U1)] bool returnValue)
	{
		if (!returnValue)
		{
			throw new InvalidOperationException();
		}
	}

	public static void CheckBoolean(int returnValue)
	{
		if (0 == returnValue)
		{
			throw new InvalidOperationException();
		}
	}

	public static void CheckNull(IntPtr returnValue)
	{
		if (IntPtr.Zero == returnValue)
		{
			throw new NullReferenceException();
		}
	}

	[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
	public static void DetachUnmanagedObject(DisposableWrapper obj)
	{
		obj.Detach();
	}

	[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
	public static void SetAutoDelete(DisposableWrapper obj, [MarshalAs(UnmanagedType.U1)] bool value)
	{
		obj.AutoDelete = value;
	}

	public static void ThrowExceptionForErrorStatus(int errorStatus)
	{
		throw new Exception((ErrorStatus)errorStatus);
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
