# Offline verification harness for the C0 Cloudinary tool

`fake_cloudinary.py` is a local stand-in for Cloudinary. It lets `CloudinaryC0Tool` be verified end to end **without a real account or credentials**.

The tool switches to it only when `C0_FAKE_CLOUDINARY_BASE` is set to a **localhost** URL; any other host is refused. The fake seeds 124 legacy assets (123 public, 1 authenticated), paginates listings, supports chunked multipart bodies, and logs every request.

```bash
python3 -I fake_cloudinary.py 18765 requests.log          # terminal 1
```
```bat
rem terminal 2 (fake credentials only)
set CLOUDINARY_CLOUD_NAME=fakecloud& set CLOUDINARY_API_KEY=fakekey& set CLOUDINARY_API_SECRET=fakesecret
set C0_FAKE_CLOUDINARY_BASE=http://localhost:18765
dotnet run --project ..\CloudinaryC0Tool -- inventory --probe-public
dotnet run --project ..\CloudinaryC0Tool -- spike
```

Results recorded 2026-10-30:
- **inventory:** 6 list GETs (paginated) plus 123 `HEAD` probes, **0 mutating requests**. The join with a synthetic CVs table gave 2 referenced, 122 orphaned and 1 database row without an asset, all correct.
- **spike:** all 10 checks PASS. Uploads were confined to `wazzuf-jobs/c0-spike/`, with `type=authenticated` for the private upload. The rename sent `to_type=authenticated` and `invalidate=true`. Destroy ran only on the 2 spike-created IDs (with invalidate). The 124 seeded assets were never touched.
- **Safety behaviours observed:** an upload that does not come back `authenticated` is reported as FAIL; cleanup refuses to delete any ID outside the spike folder; listing errors abort with exit code 3 before any further action.

The fake proves the tool's request logic. It cannot prove real Cloudinary free-tier behaviour (the PDF delivery block, real CDN invalidation timing), which is why the owner still runs the real spike.
