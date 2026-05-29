using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// Build a GIF from a folder of PNG frames using ffmpeg.
//
// file_pattern: "*.png" auto-detects sequence (works without glob support)
//
// boomerang  : if true, plays forward then backward in a loop
// hold_sec   : seconds to hold the last frame before reversing
//
// Two-pass palette pipeline for clean colors.

public class Script_Instance : GH_ScriptInstance
{
  private bool _lastRun = false;

  private void RunScript(
    string input_folder,
    string output_gif,
    int fps,
    string file_pattern,
    string ffmpeg_path,
    int width,
    bool boomerang,
    double hold_sec,
    bool run,
    ref object out_gif_path,
    ref object out_log)
  {
    List<string> log = new List<string>();
    out_log = log;
    out_gif_path = "";

    if (fps <= 0) fps = 30;
    if (string.IsNullOrEmpty(file_pattern)) file_pattern = "*.png";
    if (string.IsNullOrEmpty(ffmpeg_path)) ffmpeg_path = "ffmpeg";
    if (width <= 0) width = -1;
    if (hold_sec < 0) hold_sec = 0;

    if (!run || _lastRun) { _lastRun = run; out_log = log; return; }
    _lastRun = run;

    if (string.IsNullOrEmpty(input_folder) || !Directory.Exists(input_folder))
    {
      log.Add("ERROR: input_folder missing");
      out_log = log; return;
    }
    if (string.IsNullOrEmpty(output_gif))
      output_gif = Path.Combine(input_folder, "out.gif");
    if (!output_gif.EndsWith(".gif")) output_gif += ".gif";

    // Detect sequence pattern
    string inputArg;
    int frameCount = 0;
    if (file_pattern.Contains("*"))
    {
      string ext = Path.GetExtension(file_pattern);
      if (string.IsNullOrEmpty(ext)) ext = ".png";
      string[] files = Directory.GetFiles(input_folder, "*" + ext);
      if (files.Length == 0)
      {
        log.Add("ERROR: no " + ext + " files in " + input_folder);
        out_log = log; return;
      }
      Array.Sort(files);
      frameCount = files.Length;
      Match m = Regex.Match(Path.GetFileName(files[0]), @"^(.*?)(\d+)(\.\w+)$");
      if (!m.Success)
      {
        log.Add("ERROR: cannot detect number pattern in '" + Path.GetFileName(files[0]) + "'");
        out_log = log; return;
      }
      string prefix = m.Groups[1].Value;
      int digitCount = m.Groups[2].Value.Length;
      string suffix = m.Groups[3].Value;
      int startNum = int.Parse(m.Groups[2].Value);
      string seqPattern = prefix + "%0" + digitCount + "d" + suffix;
      inputArg = "-start_number " + startNum + " -i \"" + Path.Combine(input_folder, seqPattern) + "\"";
      log.Add("detected: " + seqPattern + " start=" + startNum + " count=" + frameCount);
    }
    else
    {
      inputArg = "-i \"" + Path.Combine(input_folder, file_pattern) + "\"";
    }

    string palettePath = Path.Combine(input_folder, "_palette.png");
    string holdFilter = (hold_sec > 0)
      ? ",tpad=stop_duration=" + hold_sec.ToString("F2").Replace(",", ".") + ":stop_mode=clone"
      : "";
    string scaleFilter = (width > 0) ? ",scale=" + width + ":-1:flags=lanczos" : "";

    // Build the filter chain WITHOUT trailing commas or empty filters
    // Tags end with [held_rev] for boomerang and [main] for one-way
    string filterPrep;
    string mainTag;
    if (boomerang)
    {
      filterPrep =
        "[0:v]split[fwd][rev_src];" +
        "[rev_src]reverse,trim=start_frame=1,setpts=PTS-STARTPTS[rev];" +
        "[fwd]null" + holdFilter + "[held];" +
        "[held][rev]concat=n=2:v=1,fps=" + fps + scaleFilter;
      mainTag = "[main]";
    }
    else
    {
      filterPrep = "[0:v]null" + holdFilter + ",fps=" + fps + scaleFilter;
      mainTag = "[main]";
    }

    Stopwatch sw = Stopwatch.StartNew();

    // Pass 1: palette (filterPrep -> palettegen)
    string args1 = "-y -framerate " + fps + " " + inputArg +
                   " -filter_complex \"" + filterPrep + ",palettegen[pal]\"" +
                   " -map \"[pal]\" \"" + palettePath + "\"";
    log.Add("pass 1 palette");
    int code1 = RunProcess(ffmpeg_path, args1, log);
    if (code1 != 0)
    {
      log.Add("ERROR: palette failed (code " + code1 + ")");
      out_log = log; return;
    }

    // Pass 2: use palette + concat + loop forever (-loop 0)
    string args2 = "-y -framerate " + fps + " " + inputArg + " -i \"" + palettePath + "\"" +
                   " -filter_complex \"" + filterPrep + mainTag + ";" + mainTag + "[1:v]paletteuse[out]\"" +
                   " -map \"[out]\" -loop 0 \"" + output_gif + "\"";
    log.Add("pass 2 gif");
    int code2 = RunProcess(ffmpeg_path, args2, log);

    sw.Stop();

    if (code2 == 0 && File.Exists(output_gif))
    {
      FileInfo fi = new FileInfo(output_gif);
      log.Add("DONE: " + output_gif + " (" + (fi.Length / 1024).ToString() + " KB, " + (sw.ElapsedMilliseconds / 1000.0).ToString("F1") + " s)");
      out_gif_path = output_gif;
    }
    else
    {
      log.Add("ERROR: gif failed (code " + code2 + ")");
    }

    try { if (File.Exists(palettePath)) File.Delete(palettePath); } catch { }

    out_log = log;
  }

  private int RunProcess(string exe, string args, List<string> log)
  {
    try
    {
      ProcessStartInfo psi = new ProcessStartInfo();
      psi.FileName = exe;
      psi.Arguments = args;
      psi.UseShellExecute = false;
      psi.CreateNoWindow = true;
      psi.RedirectStandardError = true;
      psi.RedirectStandardOutput = true;
      Process p = Process.Start(psi);
      string stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();
      if (p.ExitCode != 0 && stderr.Length > 0)
      {
        string snippet = stderr.Length > 400 ? stderr.Substring(stderr.Length - 400) : stderr;
        log.Add("stderr: " + snippet);
      }
      return p.ExitCode;
    }
    catch (Exception ex)
    {
      log.Add("exception: " + ex.Message);
      return -1;
    }
  }
}
