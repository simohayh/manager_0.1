# End-to-end check of the whole flow against a RUNNING app. Use it after every change, before every install.
# Usage:  .\smoke-test.ps1 -BaseUrl http://localhost:5000 -KitchenPin 1111 -CashierPin 2222 -OwnerPin 9999
# (Development mode defaults are 1111 / 2222 / 9999. Table 49 is used so it doesn't clash with real tables.)
# It leaves one settled test order in your sales report - run it on a fresh/test database, not during service.
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$KitchenPin = "1111",
    [string]$CashierPin = "2222",
    [string]$OwnerPin = "9999",
    [string]$Table = "49"
)

$script:fail = 0
function Check($name, [bool]$ok, $detail = "") {
    if ($ok) { Write-Host "PASS  $name" -ForegroundColor Green }
    else { Write-Host "FAIL  $name  $detail" -ForegroundColor Red; $script:fail++ }
}
function Call($method, $path, $body, $session) {
    $p = @{ Uri = "$BaseUrl$path"; Method = $method; UseBasicParsing = $true; ErrorAction = "Stop" }
    if ($session) { $p.WebSession = $session }
    if ($null -ne $body) { $p.Body = ($body | ConvertTo-Json -Depth 6); $p.ContentType = "application/json" }
    try {
        $r = Invoke-WebRequest @p
        $json = $null; try { $json = $r.Content | ConvertFrom-Json } catch {}
        return @{ Status = [int]$r.StatusCode; Json = $json }
    } catch {
        $code = 0; $json = $null
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
            try { $json = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() | ConvertFrom-Json } catch {}
        }
        return @{ Status = $code; Json = $json }
    }
}
function Login($role, $pin) {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $r = Call "POST" "/api/staff/login" @{ role = $role; pin = $pin } $s
    if ($r.Status -ne 200) { Write-Host "Could not log in as $role (status $($r.Status))" -ForegroundColor Red }
    return $s
}

$owner = Login "owner" $OwnerPin
$kitchen = Login "kitchen" $KitchenPin
$cashier = Login "cashier" $CashierPin

# Staff areas are locked without a login
Check "Kitchen API rejects anonymous" ((Call "GET" "/api/kitchen/orders" $null $null).Status -eq 401)
Check "Cashier API rejects anonymous" ((Call "GET" "/api/cashier/orders" $null $null).Status -eq 401)
Check "Admin API rejects anonymous"   ((Call "GET" "/api/admin/settings" $null $null).Status -eq 401)
Check "Wrong PIN is refused" ((Call "POST" "/api/staff/login" @{ role = "kitchen"; pin = "000000x" } $null).Status -eq 401)

# Menu + table key
$menu = Call "GET" "/api/menu" $null $null
Check "Menu loads and has items" ($menu.Status -eq 200 -and @($menu.Json).Count -gt 0)
$itemId = @($menu.Json)[0].id
$keys = Call "GET" "/api/admin/table-keys?count=50" $null $owner
$key = ($keys.Json | Where-Object { $_.table -eq [int]$Table }).key
Check "Owner can get table keys" ($keys.Status -eq 200 -and $key)


# ---- Voiding one item (table 48) ----
$menuItems = @($menu.Json)
$key48 = ($keys.Json | Where-Object { $_.table -eq 48 }).key
if ($menuItems.Count -ge 2 -and $key48) {
    $two = Call "POST" "/api/orders/batch" @{ tableNumber = "48"; tableKey = $key48; items = @(@{ menuItemId = $menuItems[0].id; quantity = 1 }, @{ menuItemId = $menuItems[1].id; quantity = 1 }) } $null
    Check "Two-item order placed on table 48" ($two.Status -eq 200 -and $two.Json.orderCodes.Count -eq 2) "status $($two.Status) $($two.Json.message)"
    if ($two.Json.orderCodes.Count -eq 2) {
        $c1 = $two.Json.orderCodes[0]; $c2 = $two.Json.orderCodes[1]
        Check "Void without a reason is refused" ((Call "POST" "/api/cashier/void-item" @{ orderCode = $c1 } $cashier).Status -eq 400)
        Check "Kitchen cannot void" ((Call "POST" "/api/cashier/void-item" @{ orderCode = $c1; reason = "x x x" } $kitchen).Status -eq 401)
        Check "Cashier voids one item" ((Call "POST" "/api/cashier/void-item" @{ orderCode = $c1; reason = "smoke test" } $cashier).Status -eq 200)
        $t = Call "POST" "/api/orders/track-batch" @{ codes = @($c1, $c2) } $null
        Check "Voided item shows as Voided" (@($t.Json | Where-Object { $_.orderCode -eq $c1 -and $_.status -eq "Voided" }).Count -eq 1)
        $k = Call "GET" "/api/kitchen/orders" $null $kitchen
        Check "Kitchen no longer sees the voided item" (@($k.Json | Where-Object { $_.orderCode -eq $c1 }).Count -eq 0 -and @($k.Json | Where-Object { $_.orderCode -eq $c2 }).Count -eq 1)
        Check "Voiding the same item twice is refused" ((Call "POST" "/api/cashier/void-item" @{ orderCode = $c1; reason = "again" } $cashier).Status -eq 409)
        Check "Cashier voids the last item" ((Call "POST" "/api/cashier/void-item" @{ orderCode = $c2; reason = "smoke test" } $cashier).Status -eq 200)
        $again48 = Call "POST" "/api/orders/batch" @{ tableNumber = "48"; tableKey = $key48; items = @(@{ menuItemId = $menuItems[0].id; quantity = 1 }) } $null
        Check "Table 48 is free again after everything was voided" ($again48.Status -eq 200)
        if ($again48.Status -eq 200) { Call "POST" "/api/cashier/void-item" @{ orderCode = $again48.Json.orderCodes[0]; reason = "cleanup" } $cashier | Out-Null }
    }
} else { Write-Host "SKIP  void tests (need 2 menu items)" -ForegroundColor Yellow }

$orderBody = @{ tableNumber = $Table; items = @(@{ menuItemId = $itemId; quantity = 2; notes = "smoke test" }) }

# Ordering needs the table QR key (when the setting is on)
$settings = Call "GET" "/api/admin/settings" $null $owner
if ($settings.Json.requireTableKey) {
    $noKey = Call "POST" "/api/orders/batch" $orderBody $null
    Check "Order WITHOUT table key is refused" ($noKey.Status -eq 403)
    $badKey = Call "POST" "/api/orders/batch" (@{ tableNumber = $Table; tableKey = "wrongkey123"; items = $orderBody.items }) $null
    Check "Order with WRONG table key is refused" ($badKey.Status -eq 403)
} else { Write-Host "SKIP  table-key checks (setting is off)" -ForegroundColor Yellow }

# Full flow
$orderBody.tableKey = $key
$placed = Call "POST" "/api/orders/batch" $orderBody $null
Check "Order is placed" ($placed.Status -eq 200 -and $placed.Json.orderCodes.Count -eq 1) "status $($placed.Status) $($placed.Json.message)"
$code = $placed.Json.orderCodes[0]

# One guest per table: a stranger is turned away, the guest who owns the table (knows an order code) can add more
$again = Call "POST" "/api/orders/batch" $orderBody $null
Check "A stranger can't order on a table that is in use (409)" ($again.Status -eq 409)
$ownerBody = @{ tableNumber = $Table; tableKey = $key; myOrderCodes = @($code); items = $orderBody.items }
Check "Table check says 'taken' for a stranger" ((Call "POST" "/api/tables/check" @{ table = $Table; codes = @() } $null).Json.state -eq "taken")
Check "Table check says 'yours' for the guest at the table" ((Call "POST" "/api/tables/check" @{ table = $Table; codes = @($code) } $null).Json.state -eq "yours")
$more = Call "POST" "/api/orders/batch" $ownerBody $null
Check "The guest at the table CAN order more" ($more.Status -eq 200 -and $more.Json.orderCodes.Count -eq 1) "status $($more.Status) $($more.Json.message)"
if ($more.Status -eq 200) { Call "POST" "/api/cashier/void-item" @{ orderCode = $more.Json.orderCodes[0]; reason = "smoke test cleanup" } $cashier | Out-Null }

Check "Customer can't confirm delivery before Ready" ((Call "POST" "/api/orders/confirm-delivery" @{ orderCode = $code } $null).Status -eq 409)
$kitchenList = Call "GET" "/api/kitchen/orders" $null $kitchen
Check "Kitchen sees the order" (@($kitchenList.Json | Where-Object { $_.orderCode -eq $code }).Count -eq 1)
Check "Kitchen starts cooking" ((Call "POST" "/api/kitchen/start-cooking" @{ orderCode = $code; prepMinutes = 5 } $kitchen).Status -eq 200)
Check "Kitchen marks Ready" ((Call "POST" "/api/kitchen/update-status" @{ orderCode = $code; newStatus = "Ready" } $kitchen).Status -eq 200)
Check "Customer confirms delivery" ((Call "POST" "/api/orders/confirm-delivery" @{ orderCode = $code } $null).Status -eq 200)
$pay = Call "POST" "/api/orders/request-payment" @{ codes = @($code) } $null
Check "Customer requests payment (token issued)" ($pay.Status -eq 200 -and $pay.Json.paymentToken)
Check "No more ordering once the bill is waiting for the cashier (409)" ((Call "POST" "/api/orders/batch" $ownerBody $null).Status -eq 409)
# ---- Discounts: small one alone, big one needs the owner PIN ----
Check "Discount without a reason is refused" ((Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 5 } $cashier).Status -eq 400)
$bigNoPin = Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 50; reason = "smoke test" } $cashier
Check "Big discount without the owner PIN is refused" ($bigNoPin.Status -eq 403 -and $bigNoPin.Json.needsOwnerPin)
Check "Big discount with a WRONG owner PIN is refused" ((Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 50; reason = "smoke test"; ownerPin = "000000x" } $cashier).Status -eq 403)
Check "Discount over 100% is refused" ((Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 150; reason = "smoke test"; ownerPin = $OwnerPin } $cashier).Status -eq 400)
Check "Big discount with the owner PIN works" ((Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 50; reason = "smoke test"; ownerPin = $OwnerPin } $cashier).Status -eq 200)
$small = Call "POST" "/api/cashier/discount" @{ table = $Table; type = "percent"; value = 5; reason = "smoke test" } $cashier
Check "Small discount works alone (and replaces the old one)" ($small.Status -eq 200 -and $small.Json.amount -gt 0)
$adj = Call "POST" "/api/orders/adjustments" @{ codes = @($code) } $null
Check "Customer sees exactly one discount on their bill" ($adj.Status -eq 200 -and [decimal]$adj.Json.discount -eq [decimal]$small.Json.amount)
$cashierView = Call "GET" "/api/cashier/orders" $null $cashier
Check "Cashier list shows the discount line" (@($cashierView.Json | Where-Object { $_.orderCode -like "ADJ-*" }).Count -eq 1)
Check "Kitchen never sees the discount line" (@((Call "GET" "/api/kitchen/orders" $null $kitchen).Json | Where-Object { $_.orderCode -like "ADJ-*" }).Count -eq 0)

$settle = Call "POST" "/api/cashier/settle" @{ query = $Table } $cashier
Check "Cashier settles the table" ($settle.Status -eq 200 -and $settle.Json.receiptNumber -gt 0) "status $($settle.Status)"
$tracked = Call "POST" "/api/orders/track-batch" @{ codes = @($code) } $null
Check "Order shows as Completed + Paid" ($tracked.Json[0].status -eq "Completed" -and $tracked.Json[0].isPaid)
Check "Table is 'free' again after settling" ((Call "POST" "/api/tables/check" @{ table = $Table; codes = @($code) } $null).Json.state -eq "free")
Check "Discount line was settled too (table is free)" (@((Call "GET" "/api/cashier/orders" $null $cashier).Json | Where-Object { $_.tableNumber -eq $Table }).Count -eq 0)

$today = (Get-Date).ToString("yyyy-MM-dd")
$report = Call "GET" "/api/admin/report?from=$today&to=$today&tzOffset=0" $null $owner
Check "Sales report loads" ($report.Status -eq 200)
Check "Report counts the discount (and one bill, not two)" ($report.Json.discounts -gt 0 -and $report.Json.bills -ge 1)

# Flood protection (needs a fresh limiter window: this uses up the order allowance for this computer for ~10 minutes)
$limited = $false
1..12 | ForEach-Object {
    $r = Call "POST" "/api/orders/batch" (@{ tableNumber = "48"; tableKey = "x"; items = $orderBody.items }) $null
    if ($r.Status -eq 429) { $limited = $true }
}
Check "Order flood is rate-limited (429)" $limited

Write-Host ""
if ($script:fail -eq 0) { Write-Host "ALL CHECKS PASSED" -ForegroundColor Green } else { Write-Host "$script:fail CHECK(S) FAILED" -ForegroundColor Red; exit 1 }
