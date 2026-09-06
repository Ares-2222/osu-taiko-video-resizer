# osu!taiko Video Resizer 2

Rebuilt encoder for the original
[osu!taiko Video Resizer](https://osu.ppy.sh/community/forums/topics/1129583)
by Khoo Hao Yit and Jerry. Same frame layout, same output dimensions.
What changed is how the video is encoded, and that is where the quality
comes from.

## Download

Grab the zip from [Releases](../../releases). It contains everything you
need including ffmpeg.

The exe is not code signed, so Windows will show a SmartScreen warning the
first time. Click **More info**, then **Run anyway**.

## Setup

1. Unzip anywhere.
2. Run `Build EXE.bat` once. It produces `osu!taiko Video Resizer.exe`
   next to it.
3. Run the exe.

`Build EXE.bat` uses the C# compiler that already ships with Windows as
part of the .NET Framework:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Nothing is downloaded, nothing is installed, no internet needed. After the
build you can delete `Build EXE.bat` and the `src` folder if you want.

## Usage

Drag videos into the window or use **Add files**. Several at once is fine.
Pick your settings, press **Start**.

Results go to the `output` folder next to the exe. Duplicate names get
numbered rather than overwritten, which is handy when you re-run the same
source at a different quality to compare.

## Settings

| Setting | What it does |
| --- | --- |
| Layout | Centre is normal. Right side pushes the video right, leaving room for a storyboarded title or lyrics. |
| Quality | CRF. Lower is better and bigger. 18 max, 20 recommended, 23 balanced, 26 small. |
| Encoder | How hard x264 works. Slower is not lower quality, it is the same quality in a smaller file. `veryslow` is around 20% smaller than `slow` at the same CRF. |
| Frame rate | Upper limits, not a forced rate. The source rate is read first and the filter is only applied when the source is above the limit. |
| Container | mp4, avi or flv. Same H.264 inside all three. Hover each for a note. |
| Blur | Strength of the blurred side strip. 0 turns it off. |
| Anime tune | `x264 -tune animation`. Helps flat shaded drawn material, leave off for live action. |
| Limit file size | A ceiling in MB, not a target. |
| osu! ranked limit | Works the ceiling out from the beatmap upload limit. |

### Frame rate

Anime is usually 23.976 fps. Forcing that up to 30 would duplicate frames
and add judder, so a cap never raises the rate. The log says what happened
to each file.

### Size limiting

The video is always encoded at the chosen CRF first. Only if the result is
over the ceiling does it get re-encoded with two passes to fit, so videos
that are naturally small stay small.

The two-pass target allows for the container's own overhead, and the result
is checked afterwards. If x264 overshot, it retries once with a tighter
target and warns in the log if it still cannot fit.

`osu! ranked limit` computes the ceiling from the upload limit of 5 MB plus
10 MB per minute of beatmap length, capped at 100 MB. That allowance covers
the whole beatmapset, so roughly 1.5 MB per minute is reserved for a 192
kbps mp3 plus 2 MB for the background and hitsounds. The log shows the
arithmetic.

Rough video budgets:

| Map length | Video budget |
| --- | --- |
| 1:30 | 16 MB |
| 2:00 | 20 MB |
| 3:00 | 28 MB |
| 5:00 | 45 MB |
| 10:00 | 83 MB |

The video's own length is used as the map length. osu! actually measures
the limit from the timestamp of the last object, so if your map ends well
before the video does, use the manual ceiling instead.

## Ranked notes

Defaults are set so the output stays rankable:

* exactly 1280x720, which is the maximum the ranking criteria allow
* H.264, since osu! does not play H.265, VP9, AV1, mkv or mov
* audio track removed
* metadata stripped

Do not change the codec or the video will not play in the map.

## What changed from the original

The old command ended with `-b:v 800K`, a fixed bitrate applied to every
video regardless of length, frame rate or content. A busy 60 fps clip got
the same 800 kbit/s as a static one. That is where the blocking came from.

1. **CRF instead of a fixed bitrate.** On a realistic test clip CRF 20 came
   out at 534 KB against the old 521 KB, so practically the same size with
   a measurably better picture. On a hard grainy 60 fps clip CRF 23 was
   both smaller (918 KB against 937 KB) and better.
2. **preset slow or veryslow instead of the default medium.** Same quality
   target, 20 to 28% smaller.
3. **lanczos scaling instead of the default bicubic.** The visible video is
   scaled down to 340 px tall, so the scaler matters. Measured sharpness of
   that region went up about 19%. The new encoded output is sharper than the
   old chain was even when encoded losslessly.
4. **gblur instead of boxblur**, with the filter chain running in 10 bit and
   dithered down to 8 bit at the end. On a smooth gradient that cut the
   average flat banding plateau from 35 px to 14 px for about 1.3% more size.
5. **Ultra wide videos no longer crash.** The old chain did `scale=1280:-1`
   then `crop=1280:340`. Anything wider than about 3.76:1 came out below 340
   px tall and ffmpeg died with `Invalid too big or non positive size for
   width '1280' or height '340'`. `force_original_aspect_ratio` fixes it.
6. **Blank720p.png is gone.** It was a fully opaque black 1280x720 image,
   and the old chain scaled the source to 1280x720 only to paint over all of
   it. The black canvas is made with `pad` now.
7. Audio and metadata stripped explicitly, `+faststart`, High profile level
   4.1, results collected in one output folder instead of a shared
   `output.mp4` that got overwritten every run, and batch processing.

Layout numbers are unchanged, so new videos line up with anything made by
the old version.

## The raw command

```
ffmpeg -y -i "input.mp4" -filter_complex "[0:v]format=yuv420p10le,split=2[bg][fg];[bg]scale=1280:340:force_original_aspect_ratio=increase:flags=lanczos,crop=w=1280:h=334:x=(iw-1280)/2:y=(ih-340)/2,gblur=sigma=10,pad=1280:720:0:386:black[base];[fg]scale=1280:340:force_original_aspect_ratio=decrease:flags=lanczos[fgs];[base][fgs]overlay=(W-w)/2:387[v]" -map "[v]" -an -map_metadata -1 -c:v libx264 -preset slow -crf 20 -pix_fmt yuv420p -profile:v high -level 4.1 -movflags +faststart -aspect 16:9 "output.mp4"
```

To cap the frame rate add `,fps=30` right after `overlay=(W-w)/2:387`, but
only if the source is above 30 fps.

Right side layout, replacing the `[fg]` and overlay parts:

```
[fg]scale=1280:260:force_original_aspect_ratio=decrease:flags=lanczos[fgs];[base][fgs]overlay=x=(1280-900)/2+900-w-(333-h)/2:y=387+(333-h)/2[v]
```

One gotcha if you edit the filter yourself: in yuv420p the `pad` filter
snaps its offset to the chroma grid, so an odd y is rounded down. The
blurred strip is 334 tall at y=386 rather than 333 at y=387, otherwise it
stops two rows short of the bottom and the sharp centre visibly sticks out
below it.

## Troubleshooting

* **Build EXE.bat reports compile errors.** Open an issue with the text.
* **Cannot find ffmpeg.exe.** The exe needs `files\ffmpeg\ffmpeg.exe` next
  to it.
* **Cannot create the output folder.** The program is somewhere Windows
  will not let it write, such as Program Files. Move it to your Desktop.
* **Encoding is slow.** Switch Encoder to `slow` or `medium`. Quality is
  set by CRF, not by the preset.

## Building from source

Only `src/Resizer.cs` and `Build EXE.bat` are in this repo. ffmpeg is not,
because a 56 MB binary does not belong in git history.

To build and run:

1. Download a static Windows build of ffmpeg from
   [ffmpeg.org](https://ffmpeg.org/download.html).
2. Put `ffmpeg.exe` at `files\ffmpeg\ffmpeg.exe`.
3. Run `Build EXE.bat`.

The source targets C# 5 so the legacy `csc.exe` in the .NET Framework can
compile it with no toolchain to install. No string interpolation, no `?.`,
no `nameof`, no expression bodied members.

## Credits

* Original osu!taiko Video Resizer and the frame layout: Khoo Hao Yit and Jerry
* Encoding is done by [FFmpeg](https://ffmpeg.org), which is GPL. Its
  licence ships in `files\ffmpeg\LICENSE.txt` inside the release zip.
