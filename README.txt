osu!taiko Video Resizer 2
Made by Ares
=========================
A rebuilt encoder for the osu!taiko Video Resizer originally made by
Khoo Hao Yit and Jerry:
https://osu.ppy.sh/community/forums/topics/1129583

Same frame layout, same 1280x720 output, same idea. What changed is the
encoder settings, and that is where the quality comes from.


HOW TO USE IT
-------------
1. Run "Build EXE.bat" once. It produces
   "osu!taiko Video Resizer.exe" in this folder.
2. Run that .exe.
3. Drag videos into the window, or use "Add files...". Several at once
   is fine.
4. Pick your settings and press Start.

Converted videos are written to the "output" folder next to the .exe
and are named output.mp4 (or .avi / .flv).
The folder is created on the first run. The "Open output folder" button
opens it.

If output.mp4 already exists the next one becomes "output (2).mp4"
rather than overwriting it, so converting several files in one go does
not destroy the earlier results.

Once the .exe is built you can delete "Build EXE.bat" and the "src"
folder. All the program needs to run is the .exe and files\ffmpeg.


ABOUT csc.exe
-------------
csc.exe is not in this archive and does not need to be. It is the C#
compiler that ships with Windows as part of the .NET Framework:

    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

"Build EXE.bat" finds it on its own and prints the path it used. Nothing
is downloaded, nothing is installed, no internet needed.


SETTINGS
--------
Layout       Centre is the normal one. Right side pushes the video to
             the right so there is room on the left for a storyboarded
             title, mapper name or lyrics. This is what the old "Extra"
             script used to do.

Quality      CRF. Lower number means better picture and a bigger file.
             18 is the most you would ever want, 20 is recommended,
             23 is a good compromise, 26 is for when you really need to
             squeeze it.

Encoder      How hard x264 works. Slower is NOT lower quality - it is
             the same quality in a smaller file. veryslow comes out
             roughly 20% smaller than slow at the same CRF.

Frame rate   "Cap at 30 fps" is the default, which is what the osu! wiki
             compression guide recommends (720p30).
             "Cap at 60 fps" keeps 60 fps where it genuinely exists and
             only pulls down higher rates.
             "Keep source" leaves the frame rate alone.

             These are upper limits, not a forced rate. The program
             reads the source frame rate first and only applies the
             filter when the source is above the limit. That matters:
             anime is usually 23.976 fps, and forcing that up to 30
             would duplicate frames and add judder on pans. The log
             shows what happened to each file.

Blur         Strength of the blurred strip behind the video. 0 turns it
             off.

Anime tune   x264 -tune animation. Helps flat-shaded drawn material,
             better left off for live action.

Container    Same H.264 video in a different wrapper. osu! plays all
             three. Hover each one in the program for a note.
               mp4  default, lightest, what the wiki guide produces.
                    A few clients crash or report "Video playback
                    failed" on mp4 (osu-stable-issues #1038), but it
                    is inconsistent and most people never see it.
               avi  fallback when mp4 crashes. Heaviest wrapper,
                    about 64 bytes per frame against 14 for mp4 -
                    a few hundred KB on a normal video.
               flv  also supported, but dead since Flash and barely
                    tested. 21 bytes per frame. Only if the other two
                    fail on your machine.
             All three keep H.264, so all three stay rankable, and
             the size limiting below works with all of them.

Limit size   A ceiling in MB. The video is always encoded at the chosen
             CRF first; only if the result is over the ceiling is it
             re-encoded with two passes to fit. Videos that are already
             small stay small - the ceiling never inflates them.
             The two-pass target allows for the container's own
             overhead, and the result is checked afterwards - if x264
             overshot, it retries once with a tighter target, and warns
             in the log if it still cannot fit.

osu! ranked  Works the ceiling out for you from the upload limit:
limit        5 MB + 10 MB per minute of beatmap length, capped at
             100 MB. That allowance covers the WHOLE beatmapset, so the
             tool reserves roughly 1.5 MB per minute for a 192 kbps mp3
             plus 2 MB for the background and hitsounds, and gives the
             rest to the video. The log shows the arithmetic.

             The video's own length is used as the map length, since the
             video is cut to the map. One caveat: osu! actually measures
             the limit from the timestamp of the last object, so if your
             map ends well before the video does, the real allowance is
             smaller than the one worked out here. Use the manual
             "Limit file size to" box in that case.

             Rough video budgets: 1:30 map -> 16 MB, 2:00 -> 20 MB,
             3:00 -> 28 MB, 5:00 -> 45 MB, 10:00 -> 83 MB.


NOTES FOR RANKED
----------------
What the tool does by default so the video stays rankable:
  - output is exactly 1280x720 (the ranking criteria allow no more);
  - H.264 in an .mp4 container - osu! does not play H.265, VP9, AV1,
    .mkv or .mov;
  - the audio track is removed;
  - metadata is stripped.

Do not change the codec or the container or the video simply will not
play in the map.


WHAT CHANGED, AND WHY
---------------------
The old command ended with:  -b:v 800K

That is a fixed bitrate applied to every video regardless of its length,
frame rate or content. A busy 60 fps clip got the same 800 kbit/s as a
static one, which is where the blocking and the mush came from.

1. CRF instead of a fixed bitrate.
   The encoder now spends bits where the picture needs them. On a
   realistic test clip, CRF 20 came out at 534 KB against the old
   521 KB - practically the same size, measurably better picture. On a
   hard grainy 60 fps clip, CRF 23 was both smaller (918 KB vs 937 KB)
   and better than the old output.

2. preset slow / veryslow instead of the default medium.
   Same quality target, file 20-28% smaller.

3. lanczos scaling instead of the default bicubic.
   The visible video is scaled down to 340 px tall, so the scaler
   matters. Measured sharpness of that region went up about 19%. The new
   encoded output is sharper than the old chain was even when encoded
   losslessly.

4. gblur instead of boxblur, and the whole filter chain runs in 10 bit
   and is dithered down to 8 bit at the end.
   On a smooth gradient this cut the average flat banding plateau from
   35 px to 14 px, for about 1.3% extra file size.

5. Ultra-wide videos no longer crash the tool.
   The old chain did scale=1280:-1 and then crop=1280:340. On anything
   wider than roughly 3.76:1 the scaled height came out below 340 and
   ffmpeg died with "Invalid too big or non positive size for width
   '1280' or height '340'". The forum thread warns about this.
   force_original_aspect_ratio fixes it properly.

6. Blank720p.png is gone.
   It was a fully opaque black 1280x720 image, and the old chain scaled
   the source to 1280x720 only to paint over all of it. The black canvas
   is now produced by pad, so that work disappears from every frame.

7. Smaller things: audio and metadata are stripped explicitly,
   +faststart, High profile level 4.1, results collected in one output
   folder instead of a single shared output.mp4 that got overwritten
   every run, and several files can be processed in one go.

The layout numbers - strip at y=387, height 340, and the offsets for the
right-side variant - are untouched, so new videos line up with anything
made by the old version.

One thing worth knowing if you edit the filter yourself: in yuv420p the
pad filter snaps its offset to the chroma grid, so an odd y is rounded
down. The blurred strip is therefore 334 tall at y=386 rather than 333
at y=387 - otherwise it stops two rows short of the bottom and the sharp
centre visibly sticks out below it.


CREDITS
-------
Version 2 - encoder pipeline, GUI and packaging: Ares.
Original osu!taiko Video Resizer, and the frame layout this version
keeps intact: Khoo Hao Yit and Jerry.
Encoding is done by FFmpeg (GPL). Its licence is in files\ffmpeg.


IF SOMETHING GOES WRONG
-----------------------
- "Build EXE.bat" reports compile errors: send the text.
- The program says it cannot find ffmpeg.exe: the .exe is in the wrong
  folder. It needs files\ffmpeg\ffmpeg.exe next to it.
- It says it cannot create the output folder: the program is somewhere
  Windows will not let it write, such as Program Files. Move the whole
  folder to your Desktop or Downloads.
- Encoding feels slow: switch Encoder to slow or medium. Quality is set
  by CRF, not by the preset.


THE RAW COMMAND
---------------
If you would rather run it by hand:

ffmpeg -y -i "input.mp4" -filter_complex "[0:v]format=yuv420p10le,split=2[bg][fg];[bg]scale=1280:340:force_original_aspect_ratio=increase:flags=lanczos,crop=w=1280:h=334:x=(iw-1280)/2:y=(ih-340)/2,gblur=sigma=10,pad=1280:720:0:386:black[base];[fg]scale=1280:340:force_original_aspect_ratio=decrease:flags=lanczos[fgs];[base][fgs]overlay=(W-w)/2:387[v]" -map "[v]" -an -map_metadata -1 -c:v libx264 -preset slow -crf 20 -pix_fmt yuv420p -profile:v high -level 4.1 -movflags +faststart -aspect 16:9 "output.mp4"

To cap the frame rate, add ,fps=30 immediately after
overlay=(W-w)/2:387 - but only if the source is actually above 30 fps.

For the right-side layout, replace the [fg] and overlay parts with:

[fg]scale=1280:260:force_original_aspect_ratio=decrease:flags=lanczos[fgs];[base][fgs]overlay=x=(1280-900)/2+900-w-(333-h)/2:y=387+(333-h)/2[v]
