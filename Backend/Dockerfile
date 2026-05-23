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
#
# Why plain symlinks don't work:
#   dlopen() resolves library names via the ldconfig cache first. The cache is
#   indexed by ELF SONAME (e.g. libleptonica-1.82.0.so.6t64 on Ubuntu 24.04),
#   NOT by the filename of our unversioned symlink. Cache miss → fallback to
#   /lib + /usr/lib only (NOT /usr/lib/x86_64-linux-gnu/ without LD_LIBRARY_PATH).
#
# Fix: create unversioned symlinks (find handles .so.6 on Debian 12 and .so.6t64
# on Ubuntu 24.04) AND set LD_LIBRARY_PATH so dlopen() scans the x86_64-linux-gnu
# directory by filename BEFORE consulting the cache.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      tesseract-ocr \
      tesseract-ocr-deu \
      tesseract-ocr-eng \
      libgdiplus \
 && find /usr/lib/x86_64-linux-gnu -maxdepth 1 -name "libleptonica-*.so.*" \
        | sort -V | tail -1 \
        | xargs -r -I{} ln -sf {} /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so \
 && find /usr/lib/x86_64-linux-gnu -maxdepth 1 -name "libtesseract.so.*" \
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
