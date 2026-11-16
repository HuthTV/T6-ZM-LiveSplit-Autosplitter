"""Usage: python -I gen_fingerprint.py <plutonium|steam|store> <t6zm.exe> <output.hpp>"""
import ctypes, ctypes.wintypes as w, hashlib, struct, subprocess, sys, time

BUILDS = {
    "plutonium": {
        "sha256": "2aff340f22f0c68bf46511c7bbc6167e4bb84524817f54cae08e8f60e6f9afc3",
        "live": False,
        "regions": [(0x46BCC0, 0x58), (0x49E860, 0x98), (0x5FE180, 0x6B), (0x8F1B89, 0x70),
                    (0x5466C5, 0x03), (0x51A9A1, 0x02), (0x51A9A7, 0x01)],
    },
    "steam": {
        "sha256": "f6f7104af2bd0c2b931ea1e739969e85845e499affbf686144c6b490b344ab1e",
        "live": True,
        "regions": [(0x7E3AE0, 0x60), (0x7E3C60, 0x40), (0x7E3840, 0x80), (0x7E3E00, 0x40),
                    (0x492020, 0x80), (0x7E117D, 0x03), (0x7E1159, 0x02), (0x7E115F, 0x01)],
    },

    "store": {
        "sha256": None,
        "live": True,
        "regions": [(0x14050EFD8, 0x80), (0x14050F002, 0x80), (0x140509D20, 0x80), (0x14050F140, 0x80),
                    (0x1402A0950, 0x40), (0x14050D060, 0x60)],
    },
}

def image_bytes(disk, address, count):
    pe = struct.unpack_from("<I", disk, 60)[0]
    sections = struct.unpack_from("<H", disk, pe + 6)[0]
    optional = struct.unpack_from("<H", disk, pe + 20)[0]
    base = struct.unpack_from("<I", disk, pe + 52)[0]
    for i in range(sections):
        s = pe + 24 + optional + i * 40
        va, size, raw = struct.unpack_from("<I", disk, s + 12)[0], struct.unpack_from("<I", disk, s + 16)[0], struct.unpack_from("<I", disk, s + 20)[0]
        if base + va <= address and address + count <= base + va + size:
            start = raw + address - base - va
            return disk[start:start + count]
    raise SystemExit(f"0x{address:X} outside image")


def find_process(path):
    """Opens the running process whose image is `path` for reading."""
    k = ctypes.WinDLL("kernel32", use_last_error=True)
    k.OpenProcess.restype = w.HANDLE
    k.ReadProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
    name = path.replace("/", "\\").rsplit("\\", 1)[-1]
    listing = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {name}", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout
    for line in listing.splitlines():
        parts = line.strip().strip('"').split('","')
        if len(parts) < 2 or parts[0].lower() != name.lower(): continue
        handle = k.OpenProcess(0x10 | 0x1000, False, int(parts[1]))
        if not handle: continue
        size = w.DWORD(1024)
        image = ctypes.create_unicode_buffer(1024)
        if k.QueryFullProcessImageNameW(handle, 0, image, ctypes.byref(size)) and image.value.lower() == path.replace("/", "\\").lower():
            return k, handle
    raise SystemExit(f"{name} from {path} is not running; start the game first")

def live_bytes(path, regions):
    k, handle = find_process(path)
    header = ctypes.create_string_buffer(0x400)
    done = ctypes.c_size_t()
    base = 0x140000000 if regions[0][0] >= 0x100000000 else 0x400000
    k.ReadProcessMemory(handle, base, header, 0x400, ctypes.byref(done))
    pe = struct.unpack_from("<I", header.raw, 60)[0]
    stamp, = struct.unpack_from("<I", header.raw, pe + 8)
    image_size, = struct.unpack_from("<I", header.raw, pe + 24 + 56)
    print(f"PE timestamp 0x{stamp:08X}, image size 0x{image_size:X}")
    def read():
        out = []
        for address, count in regions:
            buffer = ctypes.create_string_buffer(count)
            done = ctypes.c_size_t()
            if not k.ReadProcessMemory(handle, address, buffer, count, ctypes.byref(done)) or done.value != count:
                raise SystemExit(f"cannot read 0x{address:X}")
            out.append(buffer.raw)
        return out
    first = read()
    time.sleep(1.0)
    if read() != first: raise SystemExit("code bytes changed while reading; they are not stable")
    return first

def main():
    build, exe, output = sys.argv[1], sys.argv[2], sys.argv[3]
    spec = BUILDS[build]
    if spec["sha256"]:
        disk = open(exe, "rb").read()
        digest = hashlib.sha256(disk).hexdigest()
        if digest != spec["sha256"]:
            raise SystemExit(f"unsupported build {digest}")
    if spec["live"]:
        data = live_bytes(exe, spec["regions"])
    else:
        data = [image_bytes(disk, a, c) for a, c in spec["regions"]]
    lines = ["#pragma once", '#include "fingerprint_region.hpp"', "", f"namespace t6r::fingerprint::{build} {{"]
    for i, block in enumerate(data):
        lines.append(f"inline constexpr std::uint8_t r{i}[] = {{{', '.join(f'0x{b:02X}' for b in block)}}};")
    entries = ", ".join(f"{{0x{a:X}, 0x{c:X}, r{i}}}" for i, (a, c) in enumerate(spec["regions"]))
    lines += [f"inline constexpr region regions[] = {{{entries}}};", "}", ""]
    open(output, "w", newline="\n").write("\n".join(lines))

main()
