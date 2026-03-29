\## WSL PyTorch smoke result



Verified in WSL on 2026-03-28:



\- `nvidia-smi` sees the RTX 5070

\- `ffmpeg` is available

\- `uv` works

\- PyTorch installed in a fresh env

\- `torch.cuda.is\_available()` returned `True`

\- device resolved as `NVIDIA GeForce RTX 5070`



Conclusion:

WSL remains a viable future host for Python-backed inference services. This is a deployment option, not a foundation requirement.

