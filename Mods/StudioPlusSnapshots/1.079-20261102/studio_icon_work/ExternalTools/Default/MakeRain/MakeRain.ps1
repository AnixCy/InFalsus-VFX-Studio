param(
 [Parameter(Mandatory=$true)][string]$RequestPath,
 [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
$request=[IO.File]::ReadAllText($RequestPath,[Text.Encoding]::UTF8)|ConvertFrom-Json
function Require([bool]$ok,[string]$message){if(!$ok){throw $message}}
function Finite([double]$v){return ![double]::IsNaN($v) -and ![double]::IsInfinity($v)}
Require ($request.version -eq 1) 'Unsupported request version.'
foreach($name in @('start','end','count','intervalMin','intervalMax','groupStart')){
 $value=[double]$request.$name
 Require ((Finite $value) -and $value -eq [Math]::Truncate($value)) "Expected an integer: $name"
}
Require ($request.start -ge 0 -and $request.end -le 86400000 -and $request.end -gt $request.start) 'Invalid time range.'
Require ($request.count -ge 1 -and $request.count -le 10000) 'Count must be 1..10000.'
Require ($request.intervalMin -ge 1 -and $request.intervalMax -ge $request.intervalMin -and $request.intervalMax -le 300000) 'Invalid duration bounds.'
Require ($request.end-$request.start -gt $request.intervalMax) 'The time range must be longer than Interval Max.'
Require ((Finite $request.xMin) -and (Finite $request.xMax) -and $request.xMin -ge -20 -and $request.xMax -le 20 -and $request.xMin -le $request.xMax) 'Invalid normalized X bounds (-20..20).'
Require ($request.groupStart -ge 0 -and [long]$request.groupStart+$request.count -le 2147483647) 'Invalid groupid range.'

# SPC uses center / split, with raw width 1 at split 100.
# Use the exact requested X interval; do not filter or move positions around tracks.
$left=[int][Math]::Ceiling([double]$request.xMin*100)
$right=[int][Math]::Floor([double]$request.xMax*100)
Require ($left -le $right) 'The X range must include at least one position at 0.01 precision.'
$random=New-Object Random
$notes=New-Object 'System.Collections.Generic.List[object]'
$recent=New-Object 'System.Collections.Generic.Queue[int]'
$headSpan=[double]($request.end-$request.start-$request.intervalMax)
for($i=0;$i -lt $request.count;$i++){
 # Stratified start times keep Count independent from the single-streak duration.
 $time=[int]$request.start+1+[int][Math]::Floor(($i+$random.NextDouble())*$headSpan/$request.count)
 $duration=$random.Next([int]$request.intervalMin,[int]$request.intervalMax+1)
 # Prefer space between recent streaks without changing X during a streak.
 $x=$random.Next($left,$right+1)
 $best=-1
 for($attempt=0;$attempt -lt 32;$attempt++){
  $candidate=$random.Next($left,$right+1)
  $distance=100000
  foreach($previous in $recent){$distance=[Math]::Min($distance,[Math]::Abs($candidate-$previous))}
  if($distance -gt $best){$best=$distance;$x=$candidate}
 }
 $recent.Enqueue($x)
 if($recent.Count -gt 14){[void]$recent.Dequeue()}
 $group=[int]$request.groupStart+$i
 # First member of each explicit groupid owns the head. Place that member off screen.
 $notes.Add([pscustomobject]@{time=$time-1;order=0;text="skyarea($($time-1),10000,100,1,10000,100,1,0,0,1,$group)"})
 $notes.Add([pscustomobject]@{time=$time;order=1;text="skyarea($time,$x,100,1,$x,100,1,0,0,$duration,$group)"})
}
$lines=@($notes|Sort-Object time,order|ForEach-Object {$_.text})
[IO.File]::WriteAllText($OutputPath,([string]::Join("`r`n",$lines)+"`r`n"),[Text.UTF8Encoding]::new($false))
