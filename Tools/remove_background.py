"""Local-only cutout worker. Images are never uploaded. First use downloads U2NETP."""
import sys
from pathlib import Path
from rembg import new_session, remove

if __name__ == "__main__":
    session = new_session("u2netp", providers=["CPUExecutionProvider"])
    result = remove(Path(sys.argv[1]).read_bytes(), session=session)
    Path(sys.argv[2]).write_bytes(result)
