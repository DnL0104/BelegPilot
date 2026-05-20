$apiBase     = "https://localhost/api/v1"
$forwardedIp = "10.20.30.40"
$pdfPath     = "$env:TEMP\uat-receipt.pdf"

# === STEP 1: Get an auth token ===
Write-Host "1/5 Authenticating..." -ForegroundColor Cyan
$creds = @{
  email       = 'uat-test@uat.de'
  displayName = 'UAT Test'
  password    = 'UatTestPass123!'
} | ConvertTo-Json

$auth = $null
try {
  $auth = Invoke-RestMethod -Uri "$apiBase/auth/register" -Method Post `
    -Headers @{ 'X-Forwarded-For' = $forwardedIp } `
    -ContentType 'application/json' -Body $creds -SkipCertificateCheck
  Write-Host "   Registered fresh user." -ForegroundColor Green
} catch {
  Write-Host "   Register failed (user probably exists), trying login..." -ForegroundColor Yellow
  try {
    $auth = Invoke-RestMethod -Uri "$apiBase/auth/login" -Method Post `
      -Headers @{ 'X-Forwarded-For' = $forwardedIp } `
      -ContentType 'application/json' `
      -Body (@{ email = 'uat-test@uat.de'; password = 'UatTestPass123!' } | ConvertTo-Json) `
      -SkipCertificateCheck
    Write-Host "   Logged in." -ForegroundColor Green
  } catch {
    Write-Host "   Login also failed: $($_.Exception.Message)" -ForegroundColor Red
    return
  }
}
$token = $auth.accessToken
Write-Host "   Token: $($token.Substring(0,40))... (length $($token.Length))" -ForegroundColor Green

# === STEP 2: Verify token on /auth/me ===
Write-Host "2/5 Verifying token..." -ForegroundColor Cyan
try {
  $me = Invoke-RestMethod -Uri "$apiBase/auth/me" -Method Get `
    -Headers @{ Authorization = "Bearer $token"; 'X-Forwarded-For' = $forwardedIp } `
    -SkipCertificateCheck
  Write-Host "   /auth/me OK — user $($me.email)" -ForegroundColor Green
} catch {
  Write-Host "   Token verification failed: $($_.Exception.Message)" -ForegroundColor Red
  return
}

# === STEP 3: Create a minimal valid PDF ===
Write-Host "3/5 Creating PDF..." -ForegroundColor Cyan
$pdf = "%PDF-1.0`n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj`n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj`n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj`nxref`n0 4`n0000000000 65535 f`n0000000009 00000 n`n0000000053 00000 n`n0000000098 00000 n`ntrailer<</Size 4/Root 1 0 R>>`nstartxref`n148`n%%EOF"
[IO.File]::WriteAllBytes($pdfPath, [Text.Encoding]::ASCII.GetBytes($pdf))
Write-Host "   PDF written ($((Get-Item $pdfPath).Length) bytes) to $pdfPath" -ForegroundColor Green

# === STEP 4: Fire 7 parallel uploads ===
Write-Host "4/5 Firing 7 concurrent uploads..." -ForegroundColor Cyan
$start = Get-Date
$results = 1..7 | ForEach-Object -Parallel {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $code = curl.exe -k -s -g -o NUL -w "%{http_code}" `
    -X POST "$using:apiBase/receipt-files" `
    -H "Authorization: Bearer $using:token" `
    -H "X-Forwarded-For: $using:forwardedIp" `
    -F "files=@$using:pdfPath" 2>&1
  $sw.Stop()
  [pscustomobject]@{ Attempt = $_; Status = $code; Ms = $sw.ElapsedMilliseconds }
} -ThrottleLimit 7
$elapsed = (Get-Date) - $start

# === STEP 5: Report ===
Write-Host "5/5 Results (wall time: $([Math]::Round($elapsed.TotalSeconds, 2))s):" -ForegroundColor Cyan
$results | Sort-Object Attempt | Format-Table -AutoSize
"Summary: " + (($results | Group-Object Status | ForEach-Object { "$($_.Count)x $($_.Name)" }) -join ', ')