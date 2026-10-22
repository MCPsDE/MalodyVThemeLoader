function Get-UiField($control,$name) { $control.GetType().GetField($name,[Reflection.BindingFlags]'Instance,NonPublic').GetValue($control) }
function Get-UiDescendants($control) { foreach($child in $control.Controls){$child; Get-UiDescendants $child} }
function Assert-UiCondition($value,$message) { if(!$value){throw $message} }
function Assert-UiFits($form,$caseName) {
    foreach($item in @(Get-UiDescendants $form)) {
        $c=$item.PSObject.BaseObject
        if(!$c.Visible){continue}
        if($c -is [Windows.Forms.Button] -and $c.Text.Length -gt 0) {
            $text=$c.Text.Replace('&','')
            $size=[Windows.Forms.TextRenderer]::MeasureText($text,$c.Font,[Drawing.Size]::Empty,[Windows.Forms.TextFormatFlags]'SingleLine,NoPadding,NoPrefix')
            $inset=[int][Math]::Ceiling(12*$c.DeviceDpi/96.0)
            $available=$c.ClientSize.Width-2*$inset-$c.Padding.Horizontal
            Assert-UiCondition ($available -ge $size.Width -and $c.ClientSize.Height-$c.Padding.Vertical-4 -ge $size.Height) "${caseName}: action label does not fit: $($c.Text), text=$size available=$available bounds=$($c.Bounds)"
            Assert-UiCondition ($c.Left -ge $c.Parent.Padding.Left-1 -and $c.Right -le $c.Parent.ClientSize.Width-$c.Parent.Padding.Right+1) "${caseName}: action exceeds parent: $($c.Text), bounds=$($c.Bounds) parent=$($c.Parent.ClientSize)"
        }
        if($c.GetType().Name -eq 'SingleLineLabel') {
            $preferred=$c.GetPreferredSize([Drawing.Size]::Empty)
            Assert-UiCondition ($c.ClientSize.Width -ge $preferred.Width-2 -and $c.Height -ge $preferred.Height) "${caseName}: navigation text does not fit: $($c.Text)"
        }
        if($c.GetType().FullName -eq 'MalodyVThemeLoader.Windows.NumberField') {
            Assert-UiCondition ($c.Height -ge [int][Math]::Ceiling(32*$c.DeviceDpi/96.0)) "${caseName}: numeric field has native fixed height"
            $native=Get-UiField $c 'editor';$inputViewport=Get-UiField $c 'inputHost';$minus=Get-UiField $c 'minus';$plus=Get-UiField $c 'plus'
            foreach($spin in @($native.Controls | Where-Object {$_.GetType().Name -like '*UpDownButtons*'})) {Assert-UiCondition (!$spin.Visible) "${caseName}: native spinner remains visible"}
            Assert-UiCondition ($minus.Left -ge $inputViewport.Right -and $plus.Left -ge $minus.Right -and $plus.Right -lt $c.Width) "${caseName}: themed steppers overlap numeric entry"
            $format='F'+$c.DecimalPlaces
            $widest=@($c.Minimum.ToString($format),$c.Maximum.ToString($format)) | ForEach-Object {[Windows.Forms.TextRenderer]::MeasureText($_,$native.Font,[Drawing.Size]::Empty,[Windows.Forms.TextFormatFlags]'SingleLine,NoPadding').Width} | Measure-Object -Maximum
            Assert-UiCondition ($inputViewport.Width -ge $widest.Maximum) "${caseName}: numeric range text is clipped"
            Assert-UiCondition ($c.Left -ge -1 -and $c.Right -le $c.Parent.ClientSize.Width+1) "${caseName}: numeric field exceeds its column: $($c.AccessibleName), bounds=$($c.Bounds), parent=$($c.Parent.Bounds) preferred=$($c.GetPreferredSize([Drawing.Size]::Empty)) grid=$($c.Parent.Parent) columns=$($c.Parent.Parent.ColumnCount) gridBounds=$($c.Parent.Parent.Bounds)"
        }
    }
}
