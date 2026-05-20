# Test A: verify the token works on a simple authenticated endpoint
"=== /auth/me ==="
curl.exe -k -i -X GET "https://localhost/api/v1/auth/me" `
  -H "Authorization: Bearer $token" `
  -H "X-Forwarded-For: 10.20.30.40"

# Test B: try /receipt-files WITH trailing slash + follow redirects
"=== /receipt-files/ WITH slash ==="
curl.exe -k -i -L -X POST "https://localhost/api/v1/receipt-files/" `
  -H "Authorization: Bearer $token" `
  -H "X-Forwarded-For: 10.20.30.40" `
  -F "files=@$env:TEMP\uat-receipt.pdf"