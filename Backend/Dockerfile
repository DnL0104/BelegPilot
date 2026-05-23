FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore TaxReader.sln
RUN dotnet publish src/TaxReader.Api/TaxReader.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Tesseract OCR with German + English language packs.
# Symlink note: libleptonica6 and libtesseract5 (pulled in by tesseract-ocr) install
# only versioned .so files (libleptonica-1.82.0.so.6, libtesseract.so.5). The
# Tesseract .NET NuGet 5.2.0 uses InteropDotNet which looks for the unversioned names
# (libleptonica-1.82.0.so, libtesseract.so) via DllImport. Create both symlinks so
# the P/Invoke resolution succeeds without adding the *-dev packages to the image.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      tesseract-ocr \
      tesseract-ocr-deu \
      tesseract-ocr-eng \
      libgdiplus \
 && ln -sf /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so.6 \
           /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so \
 && ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 \
           /usr/lib/x86_64-linux-gnu/libtesseract.so \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TaxReader.Api.dll"]
