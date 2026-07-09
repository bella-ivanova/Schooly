"""
Starts the Pix2Text FastAPI server on port 8503.
Called automatically by the C# app — do not run manually.
"""
from pix2text.serve import start_server

start_server(
    p2t_config={},
    output_md_root_dir="./pix2text_output",
    host="0.0.0.0",
    port=8503,
)
