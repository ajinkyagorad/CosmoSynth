#!/usr/bin/env python3
"""Per-level QualitySettings antiAliasing from a BUILT APK's globalgamemanagers (no UnityPy).

Verbatim from docs/research/2026-09-11-render-xr-audit.md Appendix B, with argument
handling. Finds each level's length-prefixed name, aligns to 4, reads fields in
QualitySettings.asset order.

Self-check: every level must reproduce the project YAML, or the scan does not count. A
binary scan that cannot be checked against a known source is a number, not a measurement.

    tools/gates/qsettings_scan.py <apk> [QualitySettings.asset]
    tools/gates/qsettings_scan.py --self-test <apk> <QualitySettings.asset>

Exit 0 when the scan decoded (and, with a YAML given, self-checked); 1 otherwise.
"""
import re
import struct
import sys
import zipfile

NAMES = ['Very Low', 'Low', 'Medium', 'High', 'Very High', 'Ultra']


def scan(apk):
    data = zipfile.ZipFile(apk).read('assets/bin/Data/globalgamemanagers')
    found = {}
    for n in NAMES:
        key = struct.pack('<I', len(n)) + n.encode()
        i = data.find(key)
        while i >= 0:
            p = (i + len(key) + 3) & ~3
            ints = struct.unpack_from('<5i', data, p)
            floats = struct.unpack_from('<6f', data, p + 20)
            rest = struct.unpack_from('<6i', data, p + 44)
            # pixelLights, shadows, shadowRes, shadowProj, cascades | dist, nearOff, splits
            # | shadowmaskMode, skinWeights, mipLimit, mipSettingsCount, aniso, antiAliasing
            if (0 <= ints[0] <= 8 and ints[1] in (0, 1, 2) and 0 <= ints[2] <= 3 and ints[3] in (0, 1)
                    and ints[4] in (1, 2, 4) and 0.0 <= floats[0] <= 1e4 and rest[1] in (0, 1, 2, 4, 255)
                    and rest[3] == 0 and 0 <= rest[4] <= 2 and rest[5] in (0, 1, 2, 4, 8)):
                found[n] = dict(pixelLightCount=ints[0], antiAliasing=rest[5])
                break
            i = data.find(key, i + 1)
    return found


def self_check(found, yaml_path):
    t = open(yaml_path).read()
    ok = True
    seen = 0
    for n, body in re.findall(
            r'    name: (.*?)\n(.*?)(?=\n    name: |\n  m_TextureMipmapLimitGroupNames|\n  m_PerPlatformDefaultQuality)',
            t, flags=re.S):
        aam = re.search(r'antiAliasing: (\d+)', body)
        plm = re.search(r'pixelLightCount: (\d+)', body)
        if not aam or not plm:
            continue
        seen += 1
        g = found.get(n)
        ok &= bool(g and g['antiAliasing'] == int(aam.group(1))
                   and g['pixelLightCount'] == int(plm.group(1)))
    # Zero levels compared is not a pass: it is a parser that matched nothing.
    return ok and seen > 0, seen


def main(argv):
    args = [a for a in argv[1:] if a != '--self-test']
    strict = '--self-test' in argv
    if not args:
        print(__doc__.strip())
        return 2
    apk = args[0]
    yaml_path = args[1] if len(args) > 1 else None
    found = scan(apk)
    for n in NAMES:
        print(n, found.get(n))
    if yaml_path:
        ok, seen = self_check(found, yaml_path)
        print('SELF-CHECK', 'PASS' if ok else 'FAIL', '(%d levels compared)' % seen)
        return 0 if ok else 1
    if strict:
        print('SELF-CHECK FAIL (no QualitySettings.asset given)')
        return 1
    return 0 if found else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
