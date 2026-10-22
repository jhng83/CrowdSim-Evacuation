#!/usr/bin/env python3
"""Independently probe/decode M10's known-frame artifact. This does not test Unity capture."""
import json
import subprocess
import sys
from pathlib import Path

path = Path(sys.argv[1]).resolve()
probe = json.loads(subprocess.check_output([
    "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries",
    "stream=codec_name,width,height,r_frame_rate,nb_read_frames", "-of", "json", str(path)], text=True))
stream = probe["streams"][0]
assert stream == {"codec_name": "mjpeg", "width": 1280, "height": 720, "r_frame_rate": "20/1", "nb_read_frames": "2"}, stream
raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"])
frame_bytes = 1280 * 720 * 3
assert len(raw) == frame_bytes * 2
for frame in range(2):
    def rgb(x, y):
        offset = frame * frame_bytes + (y * 1280 + x) * 3
        return raw[offset:offset + 3]
    r, g, b = rgb(100, 100)
    assert r > 230 and b < 25 and (g > 230 if frame else g < 25), (frame, "top", (r, g, b))
    r, g, b = rgb(100, 600)
    assert b > 230 and r < 25 and (g > 230 if frame else g < 25), (frame, "bottom", (r, g, b))
print("PASS: ffprobe/ffmpeg independently decoded 2 oriented 1280x720 MJPEG frames at 20/1 fps with correct top/bottom colours.")
