FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore TaxReader.sln
RUN dotnet publish src/TaxReader.Api/TaxReader.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Tesseract OCR with German + English language packs.
#
# Symlink note: the Tesseract .NET NuGet 5.2.0 (InteropDotNet) resolves native
# libraries via dlopen() using the unversioned names:
#   libleptonica-1.82.0.so   and   libtesseract.so
#
# Runtime apt packages only ship versioned SONAME symlinks:
#   Debian 12:       libleptonica-1.82.0.so.6    libtesseract.so.5
#   Ubuntu 24.04:    libleptonica-1.82.0.so.6t64  libtesseract.so.5
#
# Use `find` to locate whatever variant is installed (handles both distros),
# then create the unversioned symlink dlopen() needs. xargs -r is a no-op
# if find returns nothing so the build fails loudly if the package is absent.
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

COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TaxReader.Api.dll"]
