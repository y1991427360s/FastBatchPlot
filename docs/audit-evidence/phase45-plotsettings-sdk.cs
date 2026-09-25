using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.DatabaseServices;

[Wrapper("AcDbPlotSettings")]
public class PlotSettings : DBObject
{
	public unsafe bool PlotTransparency
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			return global::<Module>.ZcDbPlotSettings.plotTransparency(GetUnmanagedObject());
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			global::<Module>.ZcDbPlotSettings.setPlotTransparency(GetUnmanagedObject(), value);
		}
	}

	public unsafe CustomScale CustomPrintScale
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0087: Expected I, but got I8
			//IL_005b: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double num2);
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double num3);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getCustomPrintScale(GetUnmanagedObject(), &num2, &num3));
				double num4 = num3;
				double num5 = num2;
				System.Runtime.CompilerServices.Unsafe.SkipInit(out CustomScale result);
				result.m_numerator = num2;
				result.m_denominator = num3;
				return result;
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num6 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num6 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num6 != 0;
					}).Invoke())
					{
					}
					if (num6 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out CustomScale result2);
					return result2;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num6);
				}
			}
		}
	}

	public unsafe PlotPaperUnit PlotPaperUnits
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (PlotPaperUnit)global::<Module>.ZcDbPlotSettings.plotPaperUnits(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return PlotPaperUnit.Inches;
		}
	}

	public unsafe PlotRotation PlotRotation
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (PlotRotation)global::<Module>.ZcDbPlotSettings.plotRotation(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return PlotRotation.Degrees000;
		}
	}

	public unsafe ShadePlotResLevel ShadePlotResLevel
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (ShadePlotResLevel)global::<Module>.ZcDbPlotSettings.shadePlotResLevel(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return ShadePlotResLevel.Draft;
		}
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0069: Expected I, but got I8
			//IL_003e: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.setShadePlotResLevel(GetUnmanagedObject(), (ZcDbPlotSettings.ShadePlotResLevel)value));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe StdScaleType StdScaleType
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (StdScaleType)global::<Module>.ZcDbPlotSettings.stdScaleType(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return StdScaleType.ScaleToFit;
		}
	}

	public unsafe PlotSettingsShadePlotType ShadePlot
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (PlotSettingsShadePlotType)global::<Module>.ZcDbPlotSettings.shadePlot(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return PlotSettingsShadePlotType.AsDisplayed;
		}
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0069: Expected I, but got I8
			//IL_003e: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.setShadePlot(GetUnmanagedObject(), (ZcDbPlotSettings.ShadePlotType)value));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe PlotType PlotType
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return (PlotType)global::<Module>.ZcDbPlotSettings.plotType(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return PlotType.Display;
		}
	}

	public unsafe Extents2d PlotPaperMargins
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_00a3: Expected I, but got I8
			//IL_0078: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				Extents2d result = default(Extents2d);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotPaperMargins(GetUnmanagedObject(), &result.m_min.x, &result.m_min.y, &result.m_max.x, &result.m_max.y));
				return result;
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out Extents2d result2);
					return result2;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe Extents2d PlotWindowArea
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_00a3: Expected I, but got I8
			//IL_0078: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				Extents2d result = default(Extents2d);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotWindowArea(GetUnmanagedObject(), &result.m_min.x, &result.m_min.y, &result.m_max.x, &result.m_max.y));
				return result;
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out Extents2d result2);
					return result2;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe Point2d PlotPaperSize
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_007a: Expected I, but got I8
			//IL_004f: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double x);
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double y);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotPaperSize(GetUnmanagedObject(), &x, &y));
				return new Point2d(x, y);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out Point2d result);
					return result;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe Point2d PlotOrigin
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_007a: Expected I, but got I8
			//IL_004f: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double x);
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double y);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotOrigin(GetUnmanagedObject(), &x, &y));
				return new Point2d(x, y);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out Point2d result);
					return result;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe double StdScale
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_006d: Expected I, but got I8
			//IL_0042: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				System.Runtime.CompilerServices.Unsafe.SkipInit(out double result);
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getStdScale(GetUnmanagedObject(), &result));
				return result;
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return 0.0;
		}
	}

	public unsafe ObjectId ShadePlotId
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_006f: Expected I, but got I8
			//IL_0044: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcDbObjectId zcDbObjectId);
				return new ObjectId(global::<Module>.ZcDbPlotSettings.shadePlotId(GetUnmanagedObject(), &zcDbObjectId));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
					System.Runtime.CompilerServices.Unsafe.SkipInit(out ObjectId result);
					return result;
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe short ShadePlotCustomDpi
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.shadePlotCustomDPI(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return 0;
		}
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0065: Expected I, but got I8
			//IL_003a: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setShadePlotCustomDPI(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool ShowPlotStyles
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.showPlotStyles(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setShowPlotStyles(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool ScaleLineweights
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.scaleLineweights(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setScaleLineweights(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool PrintLineweights
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.printLineweights(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setPrintLineweights(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool PlotViewportBorders
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotViewportBorders(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setPlotViewportBorders(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool PlotPlotStyles
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotPlotStyles(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setPlotPlotStyles(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool PlotHidden
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotHidden(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setPlotHidden(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool DrawViewportsFirst
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.drawViewportsFirst(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				global::<Module>.ZcDbPlotSettings.setDrawViewportsFirst(GetUnmanagedObject(), value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe bool PlotWireframe
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotWireframe(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
	}

	public unsafe bool UseStandardScale
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.useStandardScale(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
	}

	public unsafe bool PlotCentered
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotCentered(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
	}

	public unsafe bool PlotAsRaster
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.plotAsRaster(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
	}

	public unsafe bool ModelType
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0064: Expected I, but got I8
			//IL_0039: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				return global::<Module>.ZcDbPlotSettings.modelType(GetUnmanagedObject());
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return false;
		}
	}

	public unsafe string PlotSettingsName
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0072: Expected I, but got I8
			//IL_0047: Expected I, but got I8
			//IL_000c: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				char* value = null;
				global::<Module>.ZcDbPlotSettings.getPlotSettingsName(GetUnmanagedObject(), &value);
				return new string(value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return null;
		}
		set
		{
			//IL_0008: Expected I8, but got I
			//IL_006c: Expected I, but got I8
			//IL_0041: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
			try
			{
				fixed (char* ptr = &global::<Module>.PtrToStringChars(value))
				{
					global::<Module>.ZcDbPlotSettings.setPlotSettingsName(GetUnmanagedObject(), ptr);
				}
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
		}
	}

	public unsafe string PlotViewName
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0076: Expected I, but got I8
			//IL_004b: Expected I, but got I8
			//IL_000c: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				char* value = null;
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotViewName(GetUnmanagedObject(), &value));
				return new string(value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return null;
		}
	}

	public unsafe string PlotConfigurationName
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0076: Expected I, but got I8
			//IL_004b: Expected I, but got I8
			//IL_000c: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				char* value = null;
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getPlotCfgName(GetUnmanagedObject(), &value));
				return new string(value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return null;
		}
	}

	public unsafe string CurrentStyleSheet
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0076: Expected I, but got I8
			//IL_004b: Expected I, but got I8
			//IL_000c: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				char* value = null;
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getCurrentStyleSheet(GetUnmanagedObject(), &value));
				return new string(value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return null;
		}
	}

	public unsafe string CanonicalMediaName
	{
		get
		{
			//IL_0008: Expected I8, but got I
			//IL_0076: Expected I, but got I8
			//IL_004b: Expected I, but got I8
			//IL_000c: Expected I, but got I8
			long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				char* value = null;
				Interop.Check((int)global::<Module>.ZcDbPlotSettings.getCanonicalMediaName(GetUnmanagedObject(), &value));
				return new string(value);
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			return null;
		}
	}

	internal new unsafe ZcDbPlotSettings* GetUnmanagedObject()
	{
		return (ZcDbPlotSettings*)base.UnmanagedObject.ToPointer();
	}

	public unsafe PlotSettings([MarshalAs(UnmanagedType.U1)] bool modelType)
	{
		//IL_000a: Expected I, but got I8
		//IL_001d: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 24uL);
		ZcDbPlotSettings* pThis = (ZcDbPlotSettings*)ptr;
		ZcDbPlotSettings* value;
		try
		{
			value = ((ptr == null) ? null : global::<Module>.ZcDbPlotSettings.{ctor}((ZcDbPlotSettings*)ptr, modelType));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDelDtor((delegate*<void*, void>)(&global::<Module>.ZcHeapOperators.delete), (void*)pThis);
			throw;
		}
		base..ctor(new IntPtr(value), autoDelete: true);
	}

	protected internal PlotSettings(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	public unsafe void AddToPlotSettingsDictionary(Database toWhichDatabase)
	{
		//IL_0008: Expected I8, but got I
		//IL_006e: Expected I, but got I8
		//IL_0043: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			Interop.Check((int)global::<Module>.ZcDbPlotSettings.addToPlotSettingsDict(GetUnmanagedObject(), toWhichDatabase.GetUnmanagedObject()));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetShadePlot(PlotSettingsShadePlotType type, ObjectId shadePlotId)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0011: Expected I, but got I8
		//IL_0016: Expected I8, but got I
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
		try
		{
			ZcDbStub* ptr = (ZcDbStub*)shadePlotId.m_id;
			System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcDbObjectId zcDbObjectId);
			*(long*)(&zcDbObjectId) = (nint)ptr;
			Interop.Check((int)global::<Module>.ZcDbPlotSettings.setShadePlot(GetUnmanagedObject(), (ZcDbPlotSettings.ShadePlotType)type, zcDbObjectId));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
