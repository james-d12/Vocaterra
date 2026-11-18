#!/usr/bin/env python3
"""B17a: compares two screenshots pixel by pixel (the built-in effect and the GLSL pass). Prints the number of pixels whose largest channel difference
exceeds each tolerance (0, 2, 8 and 16 of 255), the largest difference and the bounding box of the pixels above 8. Needs Pillow.

  python3 scripts/gl-compare-images.py a.png b.png [diff.png]

Exit status 0 when at most 0.1 percent of the pixels differ by more than 8 of 255 (the spike's tolerance), 1 otherwise."""
import sys
from PIL import Image, ImageChops

a = Image.open(sys.argv[1]).convert("RGB")
b = Image.open(sys.argv[2]).convert("RGB")
if a.size != b.size:
    print(f"sizes differ: {a.size} and {b.size}")
    sys.exit(2)
diff = ImageChops.difference(a, b)
w, h = diff.size
counts = {0: 0, 2: 0, 8: 0, 16: 0}
largest = 0
box = [w, h, -1, -1]
marked = Image.new("RGB", (w, h), (0, 0, 0))
src = diff.load()
out = marked.load()
for y in range(h):
    for x in range(w):
        m = max(src[x, y])
        largest = max(largest, m)
        for t in counts:
            if m > t:
                counts[t] += 1
        if m > 8:
            box = [min(box[0], x), min(box[1], y), max(box[2], x), max(box[3], y)]
            out[x, y] = (255, 0, 0)
total = w * h
print(f"{total} pixels; differing by more than 0: {counts[0]}, 2: {counts[2]}, 8: {counts[8]} ({100 * counts[8] / total:.4f}%), 16: {counts[16]}; largest difference {largest}/255; box of the pixels above 8: {box if counts[8] else 'none'}")
if len(sys.argv) > 3:
    marked.save(sys.argv[3])
sys.exit(0 if counts[8] <= total / 1000 else 1)
