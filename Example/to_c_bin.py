import sys

def bin2c(infile, outfile, varname="firmware"):
    with open(infile, "rb") as f:
        data = f.read()
    with open(outfile, "w") as f:
        f.write(f"unsigned char {varname}[] = {{\n")
        for i, byte in enumerate(data):
            if i % 16 == 0:
                f.write("  ")
            f.write(f"0x{byte:02X}")
            if i < len(data)-1:
                f.write(", ")
            if (i+1) % 16 == 0:
                f.write("\n")
        f.write("\n};\n")
        f.write(f"unsigned int {varname}_len = {len(data)};\n")

if __name__ == "__main__":
    bin2c('routes.bin', 'data.bin', 'g_MetroData')