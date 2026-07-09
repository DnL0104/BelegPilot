FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore TaxReader.sln
RUN dotnet publish src/TaxReader.Api/TaxReader.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Tesseract OCR with German + English language packs.
#
# InteropDotNet (bundled with Tesseract .NET 5.2.0) calls dlopen() with the
# unversioned names:  libleptonica-1.82.0.so   libtesseract.so
# These target names are fixed by the compiled NuGet interop and do NOT change
# across base-image updates.
#
# Why plain symlinks don't work:
#   dlopen() resolves library names via the ldconfig cache first. The cache is
#   indexed by ELF SONAME, NOT by the filename of our unversioned symlink.
#   Cache miss -> fallback to /lib + /usr/lib only (NOT /usr/lib/x86_64-linux-gnu/
#   without LD_LIBRARY_PATH).
#
# Why the *source* filename is matched by substring, not exact prefix:
#   The actual installed filename has changed twice already as Ubuntu renamed
#   the apt package: libleptonica6 -> libleptonica6t64 -> liblept5 (current,
#   ships liblept.so.5 / liblept.so.5.0.4 - no "libleptonica" prefix at all).
#   Matching on the substring "lept" (the library's own upstream name, stable
#   across repackaging) instead of a specific package-derived prefix survives
#   future renames without needing another hardcoded-name fix. Same reasoning
#   applies to "tesseract" defensively, even though that name hasn't moved yet.
#
# Fix: create unversioned symlinks by substring-matching whatever file is
# actually present, AND set LD_LIBRARY_PATH so dlopen() scans the
# x86_64-linux-gnu directory by filename BEFORE consulting the ldconfig cache.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      tesseract-ocr \
      tesseract-ocr-deu \
      tesseract-ocr-eng \
      libgdiplus \
 && find /usr/lib/x86_64-linux-gnu -maxdepth 1 -iname "*lept*.so.*" \
        | sort -V | tail -1 \
        | xargs -r -I{} ln -sf {} /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so \
 && find /usr/lib/x86_64-linux-gnu -maxdepth 1 -iname "*tesseract*.so.*" \
        | sort -V | tail -1 \
        | xargs -r -I{} ln -sf {} /usr/lib/x86_64-linux-gnu/libtesseract.so \
 && rm -rf /var/lib/apt/lists/*

# LD_LIBRARY_PATH is searched before the ldconfig cache, so dlopen() finds
# libleptonica-1.82.0.so and libtesseract.so in /usr/lib/x86_64-linux-gnu/.
ENV LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu

COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TaxReader.Api.dll"]
