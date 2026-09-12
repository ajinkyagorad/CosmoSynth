"""List a remote zip's members with two HTTP range requests (EOCD, then central directory)."""
import struct, sys, urllib.request
url = sys.argv[1]
def rng(a, b):
    r = urllib.request.Request(url, headers={"Range": f"bytes={a}-{b}"})
    return urllib.request.urlopen(r, timeout=60).read()
size = int(urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=60).headers["Content-Length"])
tail = rng(max(0, size - 65536), size - 1)
i = tail.rfind(b"PK\x05\x06")
n, cd_size, cd_off = struct.unpack("<HII", tail[i + 10:i + 20])
cd = rng(cd_off, cd_off + cd_size - 1)
p = 0
for _ in range(n):
    comp, unc, nl, el, cl = struct.unpack("<II HHH", cd[p + 20:p + 34])
    name = cd[p + 46:p + 46 + nl].decode()
    print(f"{unc/1e6:9.2f} MB  (zip {comp/1e6:7.2f})  {name}")
    p += 46 + nl + el + cl
