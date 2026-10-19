using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using ITHelpdeskToolkit.Models;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace ITHelpdeskToolkit.Services
{
    /// <summary>
    /// Engine used to convert legacy .xls workbooks to .xlsx. Both engines run fully locally and
    /// neither requires Microsoft Excel to be installed.
    /// </summary>
    public enum ExcelConvertEngine
    {
        /// <summary>Pure .NET conversion via the NPOI library. No external program required;
        /// fastest and most portable, but a best-effort structural copy (see conversion notes).</summary>
        Npoi,

        /// <summary>Shells out to a headless LibreOffice (soffice --headless --convert-to).
        /// Requires LibreOffice to be installed, but gives the highest-fidelity conversion since
        /// it uses a real office suite's rendering/conversion engine.</summary>
        LibreOffice
    }

    public static class ExcelRepairService
    {
        public static async Task<(bool Success, string Output)> KillExcelAsync()
        {
            return await ShellService.KillProcessAsync("EXCEL.EXE");
        }

        public static (bool Success, string Output) OpenExcelSafeMode()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "excel.exe",
                    Arguments = "/safe",
                    UseShellExecute = true
                });
                return (true, "Excel Safe Mode launch requested.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static List<string> GetOfficeVersionKeys()
        {
            var versions = new List<string>();
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office");
                if (key != null)
                {
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        if (double.TryParse(subName, out _))
                        {
                            versions.Add(subName);
                        }
                    }
                }
            }
            catch { }
            return versions.Count > 0 ? versions : new List<string> { "16.0" };
        }

        public static async Task<(bool Success, string Output)> ClearExcelLockFilesAsync()
        {
            return await Task.Run(() =>
            {
                int removed = 0;
                int failed = 0;
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                foreach (string sub in new[] { "Documents", "Desktop", "Downloads" })
                {
                    folders.Add(Path.Combine(home, sub));
                }
                folders.Add(Path.GetTempPath());

                foreach (string folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    try
                    {
                        var dirInfo = new DirectoryInfo(folder);
                        foreach (FileInfo file in dirInfo.GetFiles("~$*.xls*"))
                        {
                            try
                            {
                                file.Delete();
                                removed++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    }
                    catch { }
                }

                return (true, $"Removed {removed} Excel lock file(s). Skipped/locked: {failed}.");
            });
        }

        public static async Task<(bool Success, string Output)> DisableComAddinsAsync()
        {
            return await Task.Run(() =>
            {
                var disabled = new List<string>();
                var roots = new List<string> { @"Software\Microsoft\Office\Excel\Addins" };
                foreach (string version in GetOfficeVersionKeys())
                {
                    roots.Add($@"Software\Microsoft\Office\{version}\Excel\Addins");
                }

                foreach (string root in roots)
                {
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(root, true);
                        if (key != null)
                        {
                            foreach (string name in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using RegistryKey? subKey = key.OpenSubKey(name, true);
                                    subKey?.SetValue("LoadBehavior", 0, RegistryValueKind.DWord);
                                    disabled.Add(name);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                if (disabled.Count > 0)
                {
                    return (true, "Disabled add-in(s): " + string.Join(", ", disabled));
                }
                return (true, "No enabled COM add-ins were found — nothing to disable.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearExcelStartupAddinsAsync()
        {
            return await Task.Run(() =>
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string xlStart = Path.Combine(appData, "Microsoft", "Excel", "XLSTART");
                if (!Directory.Exists(xlStart))
                {
                    return (true, "No XLSTART folder found — nothing to disable.");
                }

                string backup = xlStart + "_disabled";
                Directory.CreateDirectory(backup);

                int moved = 0;
                try
                {
                    var dirInfo = new DirectoryInfo(xlStart);
                    foreach (FileInfo file in dirInfo.GetFiles())
                    {
                        try
                        {
                            file.MoveTo(Path.Combine(backup, file.Name));
                            moved++;
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                if (moved > 0)
                {
                    return (true, $"Moved {moved} startup add-in file(s) to:\n{backup}\n(restore them there if Excel needs them again).");
                }
                return (true, "XLSTART folder was already empty — nothing to disable.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetExcelToolbarAsync()
        {
            return await Task.Run(() =>
            {
                var changed = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                        changed.Add(version);
                    }
                    catch { }
                }

                if (changed.Count > 0)
                {
                    return (true, $"Reset toolbar/ribbon customizations for Office version(s): {string.Join(", ", changed)}.");
                }
                return (true, "No customized toolbar/ribbon settings were found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearExcelMruAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\File MRU";
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                        cleared.Add(version);
                    }
                    catch { }
                }

                if (cleared.Count > 0)
                {
                    return (true, $"Cleared recent files list for Office version(s): {string.Join(", ", cleared)}.");
                }
                return (true, "No recent files list entries were found — nothing to clear.");
            });
        }

        public static async Task<(bool Success, string Output)> RepairOfficeQuickAsync()
        {
            string commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string clickToRun = Path.Combine(commonFiles, "Microsoft Shared", "ClickToRun", "OfficeClickToRun.exe");

            if (!File.Exists(clickToRun))
            {
                return (false, "Office Click-to-Run was not found on this PC. Quick Repair only works for Microsoft 365 / Click-to-Run installs.");
            }

            string command = $"\"{clickToRun}\" scenario=Repair platform=x64 culture=en-us RepairType=QuickRepair DisplayLevel=True";
            return await ShellService.RunCommandAsync(command, 300);
        }

        public static async Task<(bool Success, string Summary, string Details)> ConvertXlsToXlsxBatchAsync(
            string[] filePaths, ExcelConvertEngine engine = ExcelConvertEngine.Npoi)
        {
            int successCount = 0;
            int failCount = 0;
            var details = new List<string>();

            foreach (string filePath in filePaths)
            {
                var (ok, msg) = await ConvertXlsToXlsxAsync(filePath, engine);
                if (ok) successCount++;
                else failCount++;
                string status = ok ? "SUCCESS" : "FAILED";
                details.Add($"[{status}] {Path.GetFileName(filePath)}\n{msg}");
            }

            string summary = $"Converted {successCount} file(s) successfully, {failCount} failed. (Engine: {engine})";
            return (successCount > 0 && failCount == 0, summary, string.Join("\n\n", details));
        }

        public static async Task<(bool Success, string Output)> ConvertXlsToXlsxAsync(
            string xlsPath, ExcelConvertEngine engine = ExcelConvertEngine.Npoi, string? outputPath = null)
        {
            return engine switch
            {
                ExcelConvertEngine.LibreOffice => await ConvertViaLibreOfficeAsync(xlsPath, outputPath),
                _ => await ConvertViaNpoiAsync(xlsPath, outputPath),
            };
        }

        private static async Task<(bool Success, string Output)> ConvertViaNpoiAsync(string xlsPath, string? outputPath)
        {
            xlsPath = Path.GetFullPath(xlsPath);
            if (!File.Exists(xlsPath)) return (false, $"File does not exist: {xlsPath}");

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.ChangeExtension(xlsPath, ".xlsx");

            return await Task.Run(() =>
            {
                try
                {
                    using FileStream inStream = new(xlsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using IWorkbook srcWorkbook = WorkbookFactory.Create(inStream);
                    using XSSFWorkbook dstWorkbook = new();

                    CopyWorkbookToXssf(srcWorkbook, dstWorkbook);

                    using (FileStream outStream = new(outputPath!, FileMode.Create, FileAccess.Write))
                    {
                        dstWorkbook.Write(outStream, false);
                    }

                    return (true,
                        $"Successfully converted using the NPOI engine (no Excel/LibreOffice install required):\n" +
                        $"  From: {xlsPath}\n  To:   {outputPath}\n\n" +
                        "Carried over: cell values, formulas, number formats, fonts, colors, fills, borders, " +
                        "alignment, merged cells, row heights, column widths, hidden rows/columns, hyperlinks, " +
                        "comments, images and print settings (paper size, margins, headers/footers, repeating " +
                        "rows/columns).\n\n" +
                        "Not carried over: pivot tables, macros/VBA, conditional-formatting rules, charts, and " +
                        "data validation. Use the LibreOffice engine instead if you need those preserved too.");
                }
                catch (Exception ex)
                {
                    return (false, $"NPOI conversion failed: {ex.Message}");
                }
            });
        }

        private static void CopyWorkbookToXssf(IWorkbook src, XSSFWorkbook dst)
        {
            var styleCache = new Dictionary<int, ICellStyle>();

            for (int s = 0; s < src.NumberOfSheets; s++)
            {
                ISheet srcSheet = src.GetSheetAt(s);
                ISheet dstSheet = dst.CreateSheet(srcSheet.SheetName);

                int maxCol = 0;
                for (int r = srcSheet.FirstRowNum; r <= srcSheet.LastRowNum; r++)
                {
                    IRow? srcRow = srcSheet.GetRow(r);
                    if (srcRow != null) maxCol = Math.Max(maxCol, (int)srcRow.LastCellNum);
                }
                for (int c = 0; c <= maxCol; c++)
                {
                    try { dstSheet.SetColumnWidth(c, srcSheet.GetColumnWidth(c)); } catch { }
                    // Hidden columns.
                    try { dstSheet.SetColumnHidden(c, srcSheet.IsColumnHidden(c)); } catch { }
                }

                for (int r = srcSheet.FirstRowNum; r <= srcSheet.LastRowNum; r++)
                {
                    IRow? srcRow = srcSheet.GetRow(r);
                    if (srcRow == null) continue;

                    IRow dstRow = dstSheet.CreateRow(r);
                    try { dstRow.HeightInPoints = srcRow.HeightInPoints; } catch { }
                    // Hidden rows.
                    try { dstRow.ZeroHeight = srcRow.ZeroHeight; } catch { }

                    for (int c = srcRow.FirstCellNum; c < srcRow.LastCellNum; c++)
                    {
                        if (c < 0) continue;
                        ICell? srcCell = srcRow.GetCell(c);
                        if (srcCell == null) continue;

                        ICell dstCell = dstRow.CreateCell(c);
                        CopyCellValue(srcCell, dstCell);
                        CopyCellStyle(dst, srcCell, dstCell, styleCache);
                        CopyHyperlink(dst, srcCell, dstCell);
                        CopyComment(src, dst, srcSheet, dstSheet, srcCell, dstCell);
                    }
                }

                for (int m = 0; m < srcSheet.NumMergedRegions; m++)
                {
                    try
                    {
                        CellRangeAddress region = srcSheet.GetMergedRegion(m);
                        dstSheet.AddMergedRegion(region);
                    }
                    catch { }
                }

                CopyImages(dst, srcSheet, dstSheet);
                CopyPrintSettings(srcSheet, dstSheet);
            }
        }

        private static void CopyHyperlink(XSSFWorkbook dstWb, ICell srcCell, ICell dstCell)
        {
            IHyperlink? link = srcCell.Hyperlink;
            if (link == null) return;

            try
            {
                IHyperlink dstLink = dstWb.GetCreationHelper().CreateHyperlink(link.Type);
                dstLink.Address = link.Address;
                dstCell.Hyperlink = dstLink;
            }
            catch { }
        }

        private static void CopyComment(IWorkbook srcWb, XSSFWorkbook dstWb, ISheet srcSheet, ISheet dstSheet, ICell srcCell, ICell dstCell)
        {
            IComment? comment = srcCell.CellComment;
            if (comment == null) return;

            try
            {
                var dstDrawing = dstSheet.DrawingPatriarch ?? dstSheet.CreateDrawingPatriarch();
                IClientAnchor anchor = dstWb.GetCreationHelper().CreateClientAnchor();
                anchor.Col1 = dstCell.ColumnIndex;
                anchor.Row1 = dstCell.RowIndex;
                anchor.Col2 = dstCell.ColumnIndex + 2;
                anchor.Row2 = dstCell.RowIndex + 3;

                IComment dstComment = dstDrawing.CreateCellComment(anchor);
                dstComment.String = dstWb.GetCreationHelper().CreateRichTextString(comment.String?.String ?? "");
                if (!string.IsNullOrEmpty(comment.Author)) dstComment.Author = comment.Author;
                dstComment.Visible = comment.Visible;
                dstCell.CellComment = dstComment;
            }
            catch { }
        }

        private static void CopyImages(XSSFWorkbook dstWb, ISheet srcSheet, ISheet dstSheet)
        {
            try
            {
                if (srcSheet.DrawingPatriarch is not System.Collections.IEnumerable shapes) return;

                foreach (object shapeObj in shapes)
                {
                    if (shapeObj is not IPicture srcPicture) continue;

                    try
                    {
                        IPictureData pictureData = srcPicture.PictureData;
                        int pictureIndex = dstWb.AddPicture(pictureData.Data, pictureData.PictureType);

                        IClientAnchor srcAnchor = srcPicture.ClientAnchor;
                        IClientAnchor dstAnchor = dstWb.GetCreationHelper().CreateClientAnchor();
                        dstAnchor.Col1 = srcAnchor.Col1;
                        dstAnchor.Row1 = srcAnchor.Row1;
                        dstAnchor.Col2 = srcAnchor.Col2;
                        dstAnchor.Row2 = srcAnchor.Row2;
                        dstAnchor.Dx1 = srcAnchor.Dx1;
                        dstAnchor.Dy1 = srcAnchor.Dy1;
                        dstAnchor.Dx2 = srcAnchor.Dx2;
                        dstAnchor.Dy2 = srcAnchor.Dy2;

                        var dstDrawing = dstSheet.DrawingPatriarch ?? dstSheet.CreateDrawingPatriarch();
                        dstDrawing.CreatePicture(dstAnchor, pictureIndex);
                    }
                    catch { /* skip pictures that can't be carried over (unsupported format, etc.) */ }
                }
            }
            catch { }
        }

        private static void CopyPrintSettings(ISheet srcSheet, ISheet dstSheet)
        {
            try
            {
                IPrintSetup srcPs = srcSheet.PrintSetup;
                IPrintSetup dstPs = dstSheet.PrintSetup;

                dstPs.PaperSize = srcPs.PaperSize;
                dstPs.Landscape = srcPs.Landscape;
                dstPs.Scale = srcPs.Scale;
                dstPs.FitWidth = srcPs.FitWidth;
                dstPs.FitHeight = srcPs.FitHeight;
                dstPs.HeaderMargin = srcPs.HeaderMargin;
                dstPs.FooterMargin = srcPs.FooterMargin;
                dstPs.NoOrientation = srcPs.NoOrientation;
                dstPs.ValidSettings = srcPs.ValidSettings;

                dstSheet.FitToPage = srcSheet.FitToPage;
                dstSheet.DisplayGridlines = srcSheet.DisplayGridlines;

                try { dstSheet.SetMargin(MarginType.LeftMargin, srcSheet.GetMargin(MarginType.LeftMargin)); } catch { }
                try { dstSheet.SetMargin(MarginType.RightMargin, srcSheet.GetMargin(MarginType.RightMargin)); } catch { }
                try { dstSheet.SetMargin(MarginType.TopMargin, srcSheet.GetMargin(MarginType.TopMargin)); } catch { }
                try { dstSheet.SetMargin(MarginType.BottomMargin, srcSheet.GetMargin(MarginType.BottomMargin)); } catch { }
                try { dstSheet.SetMargin(MarginType.HeaderMargin, srcSheet.GetMargin(MarginType.HeaderMargin)); } catch { }
                try { dstSheet.SetMargin(MarginType.FooterMargin, srcSheet.GetMargin(MarginType.FooterMargin)); } catch { }

                try
                {
                    if (srcSheet.RepeatingRows != null) dstSheet.RepeatingRows = srcSheet.RepeatingRows;
                    if (srcSheet.RepeatingColumns != null) dstSheet.RepeatingColumns = srcSheet.RepeatingColumns;
                }
                catch { }

                try
                {
                    dstSheet.Header.Left = srcSheet.Header.Left;
                    dstSheet.Header.Center = srcSheet.Header.Center;
                    dstSheet.Header.Right = srcSheet.Header.Right;
                    dstSheet.Footer.Left = srcSheet.Footer.Left;
                    dstSheet.Footer.Center = srcSheet.Footer.Center;
                    dstSheet.Footer.Right = srcSheet.Footer.Right;
                }
                catch { }
            }
            catch { }
        }

        private static void CopyCellValue(ICell src, ICell dst)
        {
            switch (src.CellType)
            {
                case CellType.String:
                    dst.SetCellValue(src.StringCellValue);
                    break;
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(src) && src.DateCellValue.HasValue)
                        dst.SetCellValue(src.DateCellValue.Value);
                    else
                        dst.SetCellValue(src.NumericCellValue);
                    break;
                case CellType.Boolean:
                    dst.SetCellValue(src.BooleanCellValue);
                    break;
                case CellType.Formula:
                    try
                    {
                        dst.SetCellFormula(src.CellFormula);
                    }
                    catch
                    {
                        // Formula couldn't carry over (e.g. references a function XSSF parses
                        // differently) — fall back to the last cached value so the cell isn't blank.
                        try
                        {
                            switch (src.CachedFormulaResultType)
                            {
                                case CellType.Numeric: dst.SetCellValue(src.NumericCellValue); break;
                                case CellType.String: dst.SetCellValue(src.StringCellValue); break;
                                case CellType.Boolean: dst.SetCellValue(src.BooleanCellValue); break;
                            }
                        }
                        catch { }
                    }
                    break;
                case CellType.Error:
                    try { dst.SetCellErrorValue(src.ErrorCellValue); } catch { }
                    break;
                case CellType.Blank:
                default:
                    break;
            }
        }

        private static void CopyCellStyle(XSSFWorkbook dstWb, ICell srcCell, ICell dstCell, Dictionary<int, ICellStyle> cache)
        {
            ICellStyle? srcStyle = srcCell.CellStyle;
            if (srcStyle == null) return;

            int key = srcStyle.Index;
            if (!cache.TryGetValue(key, out ICellStyle? dstStyle))
            {
                dstStyle = dstWb.CreateCellStyle();
                try { dstStyle.Alignment = srcStyle.Alignment; } catch { }
                try { dstStyle.VerticalAlignment = srcStyle.VerticalAlignment; } catch { }
                try { dstStyle.WrapText = srcStyle.WrapText; } catch { }
                try { dstStyle.DataFormat = dstWb.CreateDataFormat().GetFormat(srcStyle.GetDataFormatString()); } catch { }
                try { dstStyle.BorderTop = srcStyle.BorderTop; } catch { }
                try { dstStyle.BorderBottom = srcStyle.BorderBottom; } catch { }
                try { dstStyle.BorderLeft = srcStyle.BorderLeft; } catch { }
                try { dstStyle.BorderRight = srcStyle.BorderRight; } catch { }
                try { dstStyle.FillPattern = srcStyle.FillPattern; } catch { }
                try { dstStyle.FillForegroundColor = srcStyle.FillForegroundColor; } catch { }
                try { dstStyle.FillBackgroundColor = srcStyle.FillBackgroundColor; } catch { }

                try
                {
                    IFont srcFont = srcCell.Sheet.Workbook.GetFontAt(srcStyle.FontIndex);
                    IFont dstFont = dstWb.CreateFont();
                    dstFont.FontName = srcFont.FontName;
                    dstFont.FontHeightInPoints = srcFont.FontHeightInPoints;
                    dstFont.IsBold = srcFont.IsBold;
                    dstFont.IsItalic = srcFont.IsItalic;
                    dstFont.Underline = srcFont.Underline;
                    dstFont.IsStrikeout = srcFont.IsStrikeout;
                    dstFont.Color = srcFont.Color;
                    dstStyle.SetFont(dstFont);
                }
                catch { }

                cache[key] = dstStyle;
            }

            dstCell.CellStyle = dstStyle;
        }

        private static async Task<(bool Success, string Output)> ConvertViaLibreOfficeAsync(string xlsPath, string? outputPath)
        {
            xlsPath = Path.GetFullPath(xlsPath);
            if (!File.Exists(xlsPath)) return (false, $"File does not exist: {xlsPath}");

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.ChangeExtension(xlsPath, ".xlsx");

            string? soffice = FindLibreOfficeExecutable();
            if (soffice == null)
            {
                return (false,
                    "LibreOffice was not found on this machine (checked Program Files and PATH).\n" +
                    "Install LibreOffice (libreoffice.org), or use the NPOI engine instead — it needs nothing installed.");
            }

            string outDir = !string.IsNullOrWhiteSpace(Path.GetDirectoryName(outputPath))
                ? Path.GetDirectoryName(outputPath)!
                : Path.GetDirectoryName(xlsPath)!;

            string profileDir = Path.Combine(Path.GetTempPath(), $"helpdesk_lo_profile_{Environment.ProcessId}_{DateTime.Now.Ticks}");

            try
            {
                Directory.CreateDirectory(profileDir);

                // A dedicated -env:UserInstallation profile keeps this run isolated from (and never
                // blocked by) any LibreOffice window the user already has open, and skips first-run
                // setup dialogs that would otherwise hang a headless run.
                string profileUri = new Uri(profileDir).AbsoluteUri;
                string args = $"--headless --norestore --convert-to xlsx --outdir \"{outDir}\" " +
                               $"\"-env:UserInstallation={profileUri}\" \"{xlsPath}\"";

                ProcessStartInfo psi = new()
                {
                    FileName = soffice,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(psi);
                if (process == null) return (false, "Failed to start soffice.exe.");

                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(90));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return (false, "LibreOffice conversion timed out after 90 seconds.");
                }

                string stdOut = (await process.StandardOutput.ReadToEndAsync()).Trim();
                string stdErr = (await process.StandardError.ReadToEndAsync()).Trim();

                string producedPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(xlsPath) + ".xlsx");
                if (!File.Exists(producedPath))
                {
                    string log = string.Join("\n", new[] { stdOut, stdErr }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    return (false, $"LibreOffice did not produce an output file.\n{log}");
                }

                if (!string.Equals(Path.GetFullPath(producedPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(producedPath, outputPath, true);
                    try { File.Delete(producedPath); } catch { }
                }

                return (true, $"Successfully converted using the LibreOffice headless engine:\n  From: {xlsPath}\n  To:   {outputPath}");
            }
            catch (Exception ex)
            {
                return (false, $"LibreOffice conversion failed: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(profileDir, true); } catch { }
            }
        }

        private static string? FindLibreOfficeExecutable()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            };

            foreach (string c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(pathEnv))
                {
                    foreach (string dir in pathEnv.Split(Path.PathSeparator))
                    {
                        string trimmed = dir.Trim().Trim('"');
                        if (trimmed.Length == 0) continue;
                        string candidate = Path.Combine(trimmed, "soffice.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            return null;
        }

        public static async Task<(bool Success, string Output)> ClearAutoRecoverCacheAsync()
        {
            return await Task.Run(() =>
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string root = Path.Combine(appData, "Microsoft", "Excel");
                if (!Directory.Exists(root))
                {
                    return (true, "No Excel AutoRecover folder found — nothing to clear.");
                }

                int removed = 0;
                int failed = 0;
                try
                {
                    foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".asd" && ext != ".xlk") continue;
                        try
                        {
                            File.Delete(file);
                            removed++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                return (true, $"Removed {removed} AutoRecover cache file(s) (.asd/.xlk). Skipped/locked: {failed}.\n" +
                               "This clears the 'we found a problem with content' recovery-loop prompt on open.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetFileAssociationsAsync()
        {
            string exePath = null!;
            try
            {
                using RegistryKey? appPathKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\EXCEL.EXE");
                exePath = appPathKey?.GetValue(null) as string ?? "";
            }
            catch { }

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return (false, "Could not locate EXCEL.EXE on this PC — is Excel installed?");
            }

            var extensions = new[] { ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv" };
            var commands = new List<string>();
            foreach (string ext in extensions)
            {
                string progId = $"Excel{ext.Replace(".", "").ToUpperInvariant()}HelpdeskFix";
                commands.Add($"assoc {ext}={progId}");
                commands.Add($"ftype {progId}=\"{exePath}\" \"%1\"");
            }

            string combined = string.Join(" && ", commands);
            var (success, output) = await ShellService.RunRepairCommandAsync(combined, 30, admin: true);
            if (success)
            {
                return (true, $"Re-associated {string.Join(", ", extensions)} with:\n{exePath}");
            }
            return (false, $"Failed to reset file associations: {output}");
        }

        public static async Task<(bool Success, string Output)> ResetPrinterBindingAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subPath, true);
                        if (key != null)
                        {
                            // Excel stores the last-used printer under a machine-specific "PD..." value
                            // and sometimes under "DefaultPrinter" on older builds.
                            foreach (string valueName in key.GetValueNames())
                            {
                                if (valueName.StartsWith("PD", StringComparison.OrdinalIgnoreCase) ||
                                    valueName.Equals("DefaultPrinter", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        key.DeleteValue(valueName, false);
                                        cleared.Add($"{version}\\{valueName}");
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (cleared.Count > 0)
                {
                    return (true, "Cleared stale printer binding value(s):\n" + string.Join("\n", cleared) +
                                  "\nExcel will fall back to the current Windows default printer on next launch.");
                }
                return (true, "No stored printer binding was found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetResiliencyKeysAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    foreach (string sub in new[] { "DisabledItems", "DocumentRecovery" })
                    {
                        string subPath = $@"Software\Microsoft\Office\{version}\Excel\Resiliency\{sub}";
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                            cleared.Add($"{version}\\Resiliency\\{sub}");
                        }
                        catch { }
                    }
                }

                if (cleared.Count > 0)
                {
                    return (true, "Cleared resiliency key(s):\n" + string.Join("\n", cleared) +
                                  "\nFixes phantom crash-recovery prompts and add-ins Excel auto-disabled after a crash.");
                }
                return (true, "No resiliency keys were found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> TrustDownloadsFolderAsync()
        {
            return await Task.Run(() =>
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                var added = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string basePath = $@"Software\Microsoft\Office\{version}\Excel\Security\Trusted Locations";
                    string locKey = $@"{basePath}\LocationHelpdesk1";
                    try
                    {
                        using RegistryKey key = Registry.CurrentUser.CreateSubKey(locKey);
                        key.SetValue("Path", downloads + @"\", RegistryValueKind.String);
                        key.SetValue("AllowSubFolders", 1, RegistryValueKind.DWord);
                        key.SetValue("Description", "Added by IT Helpdesk Toolkit", RegistryValueKind.String);
                        added.Add(version);
                    }
                    catch { }
                }

                if (added.Count > 0)
                {
                    return (true, $"Added Downloads folder as a trusted location for Office version(s): {string.Join(", ", added)}.\n" +
                                  "Files opened from there will skip Protected View read-only mode.");
                }
                return (false, "Could not add a trusted location — no Office version keys were found.");
            });
        }

        public static async Task<(bool Success, string Output)> UnblockDownloadedFilesAsync()
        {
            return await Task.Run(() =>
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                if (!Directory.Exists(downloads))
                {
                    return (true, "Downloads folder not found — nothing to unblock.");
                }

                int unblocked = 0;
                int failed = 0;
                try
                {
                    var dirInfo = new DirectoryInfo(downloads);
                    foreach (FileInfo file in dirInfo.GetFiles("*.xls*"))
                    {
                        string zoneIdentifier = file.FullName + ":Zone.Identifier";
                        try
                        {
                            if (File.Exists(zoneIdentifier))
                            {
                                File.Delete(zoneIdentifier);
                            }
                            unblocked++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                return (true, $"Removed the 'downloaded from the internet' flag (Mark-of-the-Web) from {unblocked} Excel file(s) in Downloads. Skipped/locked: {failed}.\n" +
                               "This clears the 'Protected View: this file came from the internet' banner.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetCalculationModeAsync()
        {
            return await Task.Run(() =>
            {
                var changed = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subPath);
                        // 0 = Manual, non-zero/absent = Automatic. Remove the override so new
                        // workbooks default back to Automatic calculation.
                        if (key.GetValue("CalculationMode") != null)
                        {
                            key.DeleteValue("CalculationMode", false);
                            changed.Add(version);
                        }
                    }
                    catch { }
                }

                if (changed.Count > 0)
                {
                    return (true, $"Cleared the Manual calculation override for Office version(s): {string.Join(", ", changed)}.\n" +
                                  "New workbooks will default to Automatic. Note: a workbook that was individually saved in Manual mode still needs Formulas > Calculation Options > Automatic set inside that file.");
                }
                return (true, "No Manual calculation override was found in the registry — nothing to reset. " +
                              "If one workbook still seems stuck, check Formulas > Calculation Options in that file.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearRibbonUiCacheAsync()
        {
            return await Task.Run(() =>
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var searchRoots = new[]
                {
                    Path.Combine(localAppData, "Microsoft", "Office"),
                };

                int removed = 0;
                int failed = 0;
                foreach (string root in searchRoots)
                {
                    if (!Directory.Exists(root)) continue;
                    try
                    {
                        foreach (string file in Directory.GetFiles(root, "*.xlb", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); removed++; } catch { failed++; }
                        }
                        // Legacy per-user command bar / ribbon customization cache.
                        foreach (string file in Directory.GetFiles(root, "Excel.officeUI", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); removed++; } catch { failed++; }
                        }
                    }
                    catch { }
                }

                if (removed > 0)
                {
                    return (true, $"Removed {removed} ribbon/toolbar cache file(s) (.xlb / Excel.officeUI). Skipped/locked: {failed}.\n" +
                                  "Fixes blank or broken ribbon icons that the registry-based ribbon reset doesn't clear.");
                }
                return (true, "No ribbon/toolbar cache files were found — nothing to clear.");
            });
        }

        public static async Task<(bool Success, string Output)> ListAddinLoadBehaviorAsync()
        {
            return await Task.Run(() =>
            {
                var lines = new List<string>();
                var roots = new List<(string Scope, string Path)>
                {
                    ("Per-user", @"Software\Microsoft\Office\Excel\Addins")
                };
                foreach (string version in GetOfficeVersionKeys())
                {
                    roots.Add(("Per-user", $@"Software\Microsoft\Office\{version}\Excel\Addins"));
                }

                foreach (var (scope, path) in roots)
                {
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path);
                        if (key == null) continue;

                        foreach (string name in key.GetSubKeyNames())
                        {
                            try
                            {
                                using RegistryKey? subKey = key.OpenSubKey(name);
                                object? loadBehaviorObj = subKey?.GetValue("LoadBehavior");
                                int loadBehavior = loadBehaviorObj is int i ? i : -1;
                                string desc = loadBehavior switch
                                {
                                    0 => "Disabled",
                                    1 => "Connected (loads on demand)",
                                    2 => "Loads at startup",
                                    3 => "Loads at startup (boot flag set)",
                                    8 => "Loads on first use, then remembers state",
                                    9 => "Disabled by user/crash detection",
                                    16 => "Disabled by Office after repeated crashes",
                                    _ => "Unknown/not set"
                                };
                                lines.Add($"[{scope}] {name} — {desc} (LoadBehavior={loadBehavior})");
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (lines.Count == 0)
                {
                    return (true, "No registered COM add-ins were found for Excel.");
                }

                return (true, "Registered Excel COM add-ins:\n" + string.Join("\n", lines) +
                              "\n\nAn add-in Excel auto-disabled after a crash usually shows LoadBehavior=9 or 16 — " +
                              "use 'Disable COM Add-ins' to fully turn off a suspect one, or re-enable it manually " +
                              "in Excel > File > Options > Add-ins once you've confirmed it isn't the cause.");
            });
        }

        public static async Task<(bool Success, string Output)> RepairCorruptWorkbookAsync(string filePath, string? outputPath = null)
        {
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) return (false, $"File does not exist: {filePath}");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string dir = Path.GetDirectoryName(filePath) ?? "";
                string name = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                outputPath = Path.Combine(dir, $"{name}_repaired{ext}");
            }

            string safeSrc = filePath.Replace("'", "''");
            string safeTarget = outputPath.Replace("'", "''");

            // CorruptLoad: 0 = Normal, 1 = RepairFile, 2 = ExtractData (values only, last resort).
            string psScript = $@"
$sourcePath = '{safeSrc}'
$targetPath = '{safeTarget}'
$excel = $null
try {{
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $wb = $excel.Workbooks.Open($sourcePath, 0, $false, 5, '', '', $true, 2, '', $true, $false, 0, $true, $false, 1)
    $wb.SaveAs($targetPath)
    $wb.Close($false)
    Write-Output 'REPAIRED_OK'
}} catch {{
    Write-Error $_.Exception.Message
}} finally {{
    if ($excel -ne $null) {{
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }}
}}
";
            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 90);
            if (success && output.Contains("REPAIRED_OK"))
            {
                return (true, $"Opened with Excel's built-in repair mode and re-saved:\n  From: {filePath}\n  To:   {outputPath}\n" +
                              "If the original is severely corrupted, formatting/macros may be lost — this recovers formulas/data first.");
            }

            return (false, $"Failed to repair workbook.\nExcel COM error: {output}");
        }

        public static async Task<(bool Success, string Output)> ScanBrokenLinksAsync(string filePath)
        {
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) return (false, $"File does not exist: {filePath}");

            string safeSrc = filePath.Replace("'", "''");

            string psScript = $@"
$sourcePath = '{safeSrc}'
$excel = $null
try {{
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.AskToUpdateLinks = $false
    $wb = $excel.Workbooks.Open($sourcePath, 0, $true)
    $links = $wb.LinkSources(1)
    if ($links -eq $null) {{
        Write-Output 'NO_LINKS_FOUND'
    }} else {{
        foreach ($link in $links) {{
            $status = $wb.LinkInfo($link, 2)
            Write-Output ""LINK|$link|status=$status""
        }}
    }}
    $wb.Close($false)
}} catch {{
    Write-Error $_.Exception.Message
}} finally {{
    if ($excel -ne $null) {{
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }}
}}
";
            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 60);
            if (!success)
            {
                return (false, $"Failed to scan for broken links.\nExcel COM error: {output}");
            }

            if (output.Contains("NO_LINKS_FOUND"))
            {
                return (true, $"No external workbook links found in {Path.GetFileName(filePath)} — nothing to fix.");
            }

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.StartsWith("LINK|"));
            var broken = new List<string>();
            var ok = new List<string>();
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                string target = parts.Length > 1 ? parts[1] : line;
                // LinkInfo status: 1 = OK, other values indicate missing/unopenable/needs update.
                bool isOk = line.Contains("status=1");
                (isOk ? ok : broken).Add(target);
            }

            StringBuilder sb = new();
            sb.AppendLine($"Scanned {Path.GetFileName(filePath)}: {ok.Count + broken.Count} external link(s) found.");
            if (broken.Count > 0)
            {
                sb.AppendLine($"\n{broken.Count} broken/unreachable link(s):");
                foreach (string b in broken) sb.AppendLine($"  ✗ {b}");
                sb.AppendLine("\nOpen Data > Edit Links in Excel to relink or break these references.");
            }
            if (ok.Count > 0)
            {
                sb.AppendLine($"\n{ok.Count} healthy link(s):");
                foreach (string o in ok) sb.AppendLine($"  ✓ {o}");
            }

            return (broken.Count == 0, sb.ToString());
        }

        public static List<RepairActionItem> GetExcelFixes()
        {
            return new List<RepairActionItem>
            {
                new(0, "Close Stuck Excel Process", "Force-close any unresponsive EXCEL.EXE process.", KillExcelAsync, false),
                new(1, "Clear Lock Files", "Remove leftover ~$ lock files from Documents, Desktop, Downloads and TEMP that block reopening after a crash.", ClearExcelLockFilesAsync, false),
                new(2, "Disable COM Add-ins", "Disable third-party COM add-ins — the most common cause of Excel crashing on startup.", DisableComAddinsAsync, false),
                new(3, "Disable Startup (XLSTART) Add-ins", "Move files out of the XLSTART auto-load folder so they stop loading automatically.", ClearExcelStartupAddinsAsync, false),
                new(4, "Reset Toolbar/Ribbon", "Reset a corrupted or broken Quick Access Toolbar / ribbon layout back to defaults.", ResetExcelToolbarAsync, false),
                new(5, "Clear Recent Files List", "Clear Excel's recently-used file list.", ClearExcelMruAsync, false),
                new(6, "Quick Repair Office", "Run Microsoft's built-in Quick Repair to fix corrupted Office/Excel program files.", RepairOfficeQuickAsync, true),
                new(7, "Clear AutoRecover Cache", "Delete stuck .asd/.xlk AutoRecover temp files that cause a recovery-prompt loop on open.", ClearAutoRecoverCacheAsync, false),
                new(8, "Reset File Associations", "Re-associate .xls/.xlsx/.xlsm/.xlsb/.csv with Excel if double-clicking opens the wrong app or nothing.", ResetFileAssociationsAsync, true),
                new(9, "Reset Printer Binding", "Clear Excel's stored last-used printer so a missing/offline printer driver can't hang startup.", ResetPrinterBindingAsync, false),
                new(10, "Reset Resiliency Keys", "Clear crash-recovery and auto-disabled-item keys that cause phantom recovery prompts or add-ins stuck disabled after a crash.", ResetResiliencyKeysAsync, false),
                new(11, "Trust Downloads Folder", "Add the Downloads folder as a trusted location so files from there skip Protected View read-only mode.", TrustDownloadsFolderAsync, false),
                new(12, "Unblock Downloaded Files", "Remove the 'downloaded from the internet' flag from Excel files in Downloads to clear the Protected View internet-file banner.", UnblockDownloadedFilesAsync, false),
                new(13, "Reset Calculation Mode", "Clear a Manual calculation override so new workbooks default back to Automatic calculation.", ResetCalculationModeAsync, false),
                new(14, "Clear Ribbon/UI Cache", "Delete .xlb / Excel.officeUI cache files that cause blank or broken ribbon icons (separate from the registry ribbon reset).", ClearRibbonUiCacheAsync, false),
                new(15, "List Add-in Load Behavior", "Diagnostic: list every registered COM add-in and its LoadBehavior state, to spot which one Excel auto-disabled after a crash.", ListAddinLoadBehaviorAsync, false),
            };
        }
    }
}
