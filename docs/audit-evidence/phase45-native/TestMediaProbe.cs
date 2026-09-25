using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.PlottingServices;
using ZwSoft.ZwCAD.Runtime;
using FastBatchPlot.Core.Printing;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(TestMediaProbe))]

public class TestMediaProbe
{
    static readonly string LogFile = @"C:\Users\ys199\Desktop\probe_media.log";

    static void Log(string msg)
    {
        File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
    }

    [CommandMethod("BP_PROBE_MEDIA", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            Log("=== START BP_PROBE_MEDIA ===");
            var doc = App.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            using (var ps = new PlotSettings(true))
            {
                var psv = PlotSettingsValidator.Current;

                // 1. Baseline: Device name
                try
                {
                    psv.SetPlotConfigurationName(ps, "DWG to PDF.pc5", null);
                    psv.RefreshLists(ps);
                    Log("Test 1: 'DWG to PDF.pc5' OK. Media count: " + psv.GetCanonicalMediaNameList(ps).Count);
                }
                catch (System.Exception ex) { Log("Test 1 FAILED: " + ex.Message); }

                // 2. Full path to standard PC5
                string stdPath = @"C:\Users\ys199\AppData\Roaming\ZWSOFT\ZWCAD\2026\zh-CN\Plotters\DWG to PDF.pc5";
                try
                {
                    psv.SetPlotConfigurationName(ps, stdPath, null);
                    psv.RefreshLists(ps);
                    Log("Test 2: Full path stdPath OK. Media count: " + psv.GetCanonicalMediaNameList(ps).Count);
                }
                catch (System.Exception ex) { Log("Test 2 FAILED: " + ex.Message); }

                // 3. Test M_PDF.pc5
                try
                {
                    psv.SetPlotConfigurationName(ps, "M_PDF.pc5", null);
                    psv.RefreshLists(ps);
                    var list = psv.GetCanonicalMediaNameList(ps);
                    Log("Test 3: 'M_PDF.pc5' OK. Media count: " + list.Count);
                    bool has1261 = false, has630 = false;
                    foreach (string name in list)
                    {
                        try
                        {
                            psv.SetCanonicalMediaName(ps, name);
                            var size = ps.PlotPaperSize;
                            if ((Math.Abs(size.X - 1261) < 1 && Math.Abs(size.Y - 594) < 1) || (Math.Abs(size.X - 594) < 1 && Math.Abs(size.Y - 1261) < 1))
                            {
                                has1261 = true;
                                Log($"  M_PDF HAS 1261x594: Name={name}, Size={size.X}x{size.Y}");
                            }
                            if ((Math.Abs(size.X - 630) < 1 && Math.Abs(size.Y - 297) < 1) || (Math.Abs(size.X - 297) < 1 && Math.Abs(size.Y - 630) < 1))
                            {
                                has630 = true;
                                Log($"  M_PDF HAS 630x297: Name={name}, Size={size.X}x{size.Y}");
                            }
                        }
                        catch { }
                    }
                    Log($"  M_PDF check result: has1261={has1261}, has630={has630}");
                }
                catch (System.Exception ex) { Log("Test 3 FAILED: " + ex.Message); }

                // 4. Test ZwPdfMediaFiles.Create
                string tempRoot = Path.Combine(Path.GetTempPath(), "FastBatchPlot", "Media");
                try
                {
                    var files = ZwPdfMediaFiles.Create(stdPath, 1261, 594, tempRoot, null);
                    Log("Test 4: ZwPdfMediaFiles.Create OK. ConfigPath=" + files.ConfigurationPath);
                    try
                    {
                        psv.SetPlotConfigurationName(ps, files.ConfigurationPath, null);
                        psv.RefreshLists(ps);
                        Log("Test 4a: psv.SetPlotConfigurationName(files.ConfigurationPath) OK!");
                    }
                    catch (System.Exception ex)
                    {
                        Log("Test 4a: psv.SetPlotConfigurationName FAILED: " + ex.GetType().FullName + ": " + ex.Message);
                    }

                    // Test 4b: Unlock files first
                    files.Dispose();
                    Log("Test 4b: files.Dispose() called (locks released). File exists: " + File.Exists(files.ConfigurationPath));
                    // re-write files without lock
                }
                catch (System.Exception ex)
                {
                    Log("Test 4: ZwPdfMediaFiles.Create FAILED: " + ex.GetType().FullName + ": " + ex.Message);
                }

                // 5. Test PC5 copied to Plotters folder
                string plottersDir = @"C:\Users\ys199\AppData\Roaming\ZWSOFT\ZWCAD\2026\zh-CN\Plotters";
                string testPc5Name = "FBP_PROBE_TEST.pc5";
                string testPc5Path = Path.Combine(plottersDir, testPc5Name);
                string testPmpPath = Path.Combine(plottersDir, "PMP Files", "FBP_PROBE_TEST.pmp");
                try
                {
                    // Copy DWG to PDF.pc5 to testPc5Path
                    File.Copy(stdPath, testPc5Path, true);
                    Log("Test 5: Copied to Plotters dir: " + testPc5Path);
                    psv.RefreshLists(ps);
                    try
                    {
                        psv.SetPlotConfigurationName(ps, testPc5Name, null);
                        psv.RefreshLists(ps);
                        Log("Test 5a: SetPlotConfigurationName('" + testPc5Name + "') OK! Media count: " + psv.GetCanonicalMediaNameList(ps).Count);
                    }
                    catch (System.Exception ex)
                    {
                        Log("Test 5a FAILED: " + ex.Message);
                    }
                    try
                    {
                        psv.SetPlotConfigurationName(ps, testPc5Path, null);
                        psv.RefreshLists(ps);
                        Log("Test 5b: SetPlotConfigurationName(fullPath) in Plotters OK! Media count: " + psv.GetCanonicalMediaNameList(ps).Count);
                    }
                    catch (System.Exception ex)
                    {
                        Log("Test 5b FAILED: " + ex.Message);
                    }
                }
                finally
                {
                    try { if (File.Exists(testPc5Path)) File.Delete(testPc5Path); } catch { }
                    try { if (File.Exists(testPmpPath)) File.Delete(testPmpPath); } catch { }
                }

                // 6. Test PC5 outside Plotters folder (in %TEMP%)
                string tempPc5 = Path.Combine(Path.GetTempPath(), "test_outside.pc5");
                try
                {
                    File.Copy(stdPath, tempPc5, true);
                    Log("Test 6: Copied to Temp: " + tempPc5);
                    try
                    {
                        psv.SetPlotConfigurationName(ps, tempPc5, null);
                        psv.RefreshLists(ps);
                        Log("Test 6: SetPlotConfigurationName(tempPc5) OK!");
                    }
                    catch (System.Exception ex)
                    {
                        Log("Test 6: SetPlotConfigurationName(tempPc5) FAILED: " + ex.GetType().FullName + ": " + ex.Message);
                    }
                }
                finally
                {
                    try { if (File.Exists(tempPc5)) File.Delete(tempPc5); } catch { }
                }
            }
            Log("=== END BP_PROBE_MEDIA ===");
        }
        catch (System.Exception ex)
        {
            Log("FATAL ERROR in Run: " + ex);
        }
    }
}
