# Rate Limiting Test Scenarios

## PowerShell Test Scripts

### Test 1: Fixed Window Rate Limit (10 requests/minute)
```powershell
# This will show first 10 succeed, then 2 get queued, rest fail with 429
1..15 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/fixed" -Method Get
        Write-Host "Request $_: SUCCESS - $($response.StatusCode)" -ForegroundColor Green
        Write-Host "  Remaining: $($response.Headers.'X-RateLimit-Remaining')"
    } catch {
        Write-Host "Request $_: FAILED - $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)"
    }
    Start-Sleep -Milliseconds 100
}
```

### Test 2: Sliding Window Rate Limit (20 requests/minute)
```powershell
Write-Host "`n=== Testing Sliding Window ===" -ForegroundColor Cyan
1..25 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/sliding" -Method Get
        $remaining = $response.Headers.'X-RateLimit-Remaining'
        Write-Host "Request $_: OK - Remaining: $remaining" -ForegroundColor Green
    } catch {
        Write-Host "Request $_: RATE LIMITED (429)" -ForegroundColor Red
    }
    Start-Sleep -Milliseconds 200
}
```

### Test 3: Token Bucket (100 tokens, +20 every 10s)
```powershell
Write-Host "`n=== Testing Token Bucket - Burst Test ===" -ForegroundColor Cyan
# First burst - should allow up to 100
1..105 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/token" -Method Get
        Write-Host "Burst Request $_: OK" -ForegroundColor Green
    } catch {
        Write-Host "Burst Request $_: RATE LIMITED (Bucket Empty)" -ForegroundColor Yellow
        break
    }
}

Write-Host "`nWaiting 10 seconds for token replenishment..."
Start-Sleep -Seconds 10

# Second burst - should allow 20 more
1..25 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/token" -Method Get
        Write-Host "After Refill $_: OK" -ForegroundColor Green
    } catch {
        Write-Host "After Refill $_: RATE LIMITED" -ForegroundColor Red
    }
}
```

### Test 4: Concurrency Limit (5 concurrent requests)
```powershell
Write-Host "`n=== Testing Concurrency Limiter ===" -ForegroundColor Cyan

# Start 10 jobs simultaneously
$jobs = 1..10 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($num)
        try {
            $start = Get-Date
            $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/concurrency" -Method Get
            $duration = ((Get-Date) - $start).TotalSeconds
            "Request $num`: SUCCESS after $([math]::Round($duration, 2))s"
        } catch {
            "Request $num`: FAILED - $($_.Exception.Message)"
        }
    } -ArgumentList $_
}

# Wait for all jobs and show results
$jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
```

### Test 5: Strict Rate Limit (3 requests/30s, no queue)
```powershell
Write-Host "`n=== Testing Strict Rate Limiter ===" -ForegroundColor Cyan
1..6 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/strict" -Method Get
        Write-Host "Request $_: OK" -ForegroundColor Green
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "Request $_: REJECTED ($statusCode)" -ForegroundColor Red
        if ($_.Exception.Response.Headers.'Retry-After') {
            Write-Host "  Retry-After: $($_.Exception.Response.Headers.'Retry-After') seconds" -ForegroundColor Yellow
        }
    }
}

Write-Host "`nWaiting 30 seconds for limit to reset..."
Start-Sleep -Seconds 30
Write-Host "Trying again after reset..."
Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/strict" -Method Get | Out-Null
Write-Host "Success! Limit has reset." -ForegroundColor Green
```

### Test 6: Per-User Rate Limiting
```powershell
Write-Host "`n=== Testing Per-User Rate Limiter ===" -ForegroundColor Cyan

# User 1
Write-Host "`nTesting User 1:" -ForegroundColor Yellow
$headers1 = @{ "X-User-Id" = "user1" }
1..7 | ForEach-Object {
    try {
        Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/per-user" -Headers $headers1 | Out-Null
        Write-Host "User1 Request $_: OK" -ForegroundColor Green
    } catch {
        Write-Host "User1 Request $_: RATE LIMITED" -ForegroundColor Red
    }
}

# User 2 (should have separate limit)
Write-Host "`nTesting User 2 (different user, separate limit):" -ForegroundColor Yellow
$headers2 = @{ "X-User-Id" = "user2" }
1..7 | ForEach-Object {
    try {
        Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/per-user" -Headers $headers2 | Out-Null
        Write-Host "User2 Request $_: OK" -ForegroundColor Green
    } catch {
        Write-Host "User2 Request $_: RATE LIMITED" -ForegroundColor Red
    }
}
```

### Test 7: Custom Rate Limit Attribute
```powershell
Write-Host "`n=== Testing Custom Rate Limit Attribute ===" -ForegroundColor Cyan
1..8 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/customratelimit/custom-limit" -Method Get
        $remaining = $response.Headers.'X-RateLimit-Remaining'
        Write-Host "Request $_: OK - Remaining: $remaining" -ForegroundColor Green
    } catch {
        Write-Host "Request $_: RATE LIMITED" -ForegroundColor Red
        $retryAfter = $_.Exception.Response.Headers.'Retry-After'
        if ($retryAfter) {
            Write-Host "  Retry-After: $retryAfter seconds" -ForegroundColor Yellow
        }
    }
}
```

### Test 8: Check Rate Limit Headers
```powershell
Write-Host "`n=== Checking Rate Limit Headers ===" -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/fixed" -Method Get
Write-Host "Rate Limit Headers:" -ForegroundColor Yellow
Write-Host "  X-RateLimit-Limit: $($response.Headers.'X-RateLimit-Limit')"
Write-Host "  X-RateLimit-Remaining: $($response.Headers.'X-RateLimit-Remaining')"
Write-Host "  X-RateLimit-Reset: $($response.Headers.'X-RateLimit-Reset')"
```

### Test 9: Disabled Rate Limit
```powershell
Write-Host "`n=== Testing Disabled Rate Limiter ===" -ForegroundColor Cyan
Write-Host "Making 50 requests to disabled endpoint (should all succeed)..."
$successCount = 0
1..50 | ForEach-Object {
    try {
        Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/disabled" -Method Get | Out-Null
        $successCount++
    } catch {
        Write-Host "Request $_: FAILED (should not happen)" -ForegroundColor Red
    }
}
Write-Host "Success: $successCount/50 requests completed" -ForegroundColor Green
```

### Complete Test Suite
```powershell
# Run all tests
function Test-RateLimiting {
    $baseUrl = "http://localhost:5000"
    
    Write-Host "================================" -ForegroundColor Cyan
    Write-Host "  RATE LIMITING TEST SUITE" -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
    
    # Test 1: Fixed Window
    Write-Host "`n[1/7] Fixed Window Rate Limit..." -ForegroundColor Yellow
    $passed = 0
    $failed = 0
    1..15 | ForEach-Object {
        try {
            Invoke-WebRequest -Uri "$baseUrl/api/ratelimiting/fixed" | Out-Null
            $passed++
        } catch {
            $failed++
        }
        Start-Sleep -Milliseconds 50
    }
    Write-Host "  Result: $passed passed, $failed rate-limited" -ForegroundColor $(if($failed -gt 0){"Green"}else{"Red"})
    
    # Test 2: Sliding Window
    Write-Host "`n[2/7] Sliding Window Rate Limit..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
    $passed = 0
    1..25 | ForEach-Object {
        try {
            Invoke-WebRequest -Uri "$baseUrl/api/ratelimiting/sliding" | Out-Null
            $passed++
        } catch { }
        Start-Sleep -Milliseconds 100
    }
    Write-Host "  Result: $passed requests succeeded" -ForegroundColor Green
    
    # Test 3: Concurrency
    Write-Host "`n[3/7] Concurrency Limiter..." -ForegroundColor Yellow
    $jobs = 1..8 | ForEach-Object {
        Start-Job -ScriptBlock {
            Invoke-WebRequest -Uri $using:baseUrl/api/ratelimiting/concurrency | Out-Null
        }
    }
    $jobs | Wait-Job -Timeout 30 | Out-Null
    $completed = ($jobs | Where-Object { $_.State -eq "Completed" }).Count
    $jobs | Remove-Job -Force
    Write-Host "  Result: $completed/8 concurrent requests handled" -ForegroundColor Green
    
    # Test 4: Strict
    Write-Host "`n[4/7] Strict Rate Limiter..." -ForegroundColor Yellow
    $passed = 0
    1..5 | ForEach-Object {
        try {
            Invoke-WebRequest -Uri "$baseUrl/api/ratelimiting/strict" | Out-Null
            $passed++
        } catch { }
    }
    Write-Host "  Result: $passed/5 requests allowed (expected: 3)" -ForegroundColor $(if($passed -eq 3){"Green"}else{"Yellow"})
    
    # Test 5: Disabled
    Write-Host "`n[5/7] Disabled Rate Limiter..." -ForegroundColor Yellow
    $passed = 0
    1..30 | ForEach-Object {
        try {
            Invoke-WebRequest -Uri "$baseUrl/api/ratelimiting/disabled" | Out-Null
            $passed++
        } catch { }
    }
    Write-Host "  Result: $passed/30 requests succeeded" -ForegroundColor $(if($passed -eq 30){"Green"}else{"Red"})
    
    # Test 6: Custom Attribute
    Write-Host "`n[6/7] Custom Rate Limit Attribute..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    $passed = 0
    1..8 | ForEach-Object {
        try {
            Invoke-WebRequest -Uri "$baseUrl/api/customratelimit/custom-limit" | Out-Null
            $passed++
        } catch { }
    }
    Write-Host "  Result: $passed/8 requests succeeded (expected: 5)" -ForegroundColor $(if($passed -eq 5){"Green"}else{"Yellow"})
    
    # Test 7: Headers Check
    Write-Host "`n[7/7] Rate Limit Headers..." -ForegroundColor Yellow
    Start-Sleep -Seconds 65  # Wait for reset
    $response = Invoke-WebRequest -Uri "$baseUrl/api/ratelimiting/fixed"
    $hasHeaders = $response.Headers.'X-RateLimit-Limit' -and 
                  $response.Headers.'X-RateLimit-Remaining' -and 
                  $response.Headers.'X-RateLimit-Reset'
    Write-Host "  Result: Headers present: $hasHeaders" -ForegroundColor $(if($hasHeaders){"Green"}else{"Red"})
    
    Write-Host "`n================================" -ForegroundColor Cyan
    Write-Host "  TEST SUITE COMPLETED" -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
}

# Uncomment to run all tests
# Test-RateLimiting
```

## cURL Commands (Unix/Linux/Mac)

```bash
# Fixed Window
for i in {1..15}; do
  curl -s -o /dev/null -w "Request $i: %{http_code}\n" http://localhost:5000/api/ratelimiting/fixed
done

# With headers
curl -i http://localhost:5000/api/ratelimiting/fixed | grep -E "X-RateLimit|Retry-After"

# Per-user
curl -H "X-User-Id: user123" http://localhost:5000/api/ratelimiting/per-user

# Concurrency (parallel)
for i in {1..10}; do
  curl http://localhost:5000/api/ratelimiting/concurrency &
done
wait
```

## Expected Results

- **Fixed Window**: First 10 succeed, next 2 queued, rest 429
- **Sliding Window**: Smoother rate limiting, ~20 succeed per minute
- **Token Bucket**: First 100 succeed (burst), then ~20 per 10 seconds
- **Concurrency**: Max 5 simultaneous, others wait or 429
- **Strict**: First 3 succeed, rest 429 immediately
- **Disabled**: All succeed regardless of count
- **Per-User**: Each user has separate counter

## Monitoring in Production

```powershell
# Monitor 429 responses
Get-Content -Path "C:\logs\api.log" -Wait | Select-String "429"

# Check rate limit headers in response
Invoke-WebRequest -Uri "http://api.example.com/endpoint" | 
    Select-Object -ExpandProperty Headers | 
    Where-Object { $_.Key -like "X-RateLimit*" }
```
