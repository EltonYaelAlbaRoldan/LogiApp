param([Parameter(Mandatory=$true)][string]$Documento,[Parameter(Mandatory=$true)][string]$Pdf,[string]$DocxFinal,[switch]$SoloPdf)
$ErrorActionPreference = 'Stop'
$word = $null
$doc = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $word.AutomationSecurity = 3
    $word.Options.SaveNormalPrompt = $false
    $doc = $word.Documents.Open($Documento, $false, $false, $false)
    Write-Output 'Documento abierto en Word'
    $null = $doc.Fields.Update()
    foreach ($indice in $doc.TablesOfContents) {
        $indice.Update()
        $indice.Range.Font.Name = 'Arial'
        $indice.Range.Font.Size = 11
        $indice.Range.Font.Color = 0
        $indice.Range.ParagraphFormat.LineSpacingRule = 0
        $indice.Range.ParagraphFormat.SpaceAfter = 4
    }
    foreach ($indice in $doc.TablesOfFigures) {
        $indice.Update()
        $indice.Range.Font.Name = 'Arial'
        $indice.Range.Font.Size = 11
        $indice.Range.Font.Color = 0
        $indice.Range.ParagraphFormat.LineSpacingRule = 0
        $indice.Range.ParagraphFormat.SpaceAfter = 4
    }
    $doc.Repaginate()
    foreach ($indice in $doc.TablesOfContents) { $indice.UpdatePageNumbers() }
    foreach ($indice in $doc.TablesOfFigures) { $indice.UpdatePageNumbers() }
    if (-not $SoloPdf) { if ($DocxFinal) { $doc.SaveAs2($DocxFinal, 16) } else { $doc.Save() } }
    $doc.ExportAsFixedFormat($Pdf, 17)
    $resultado = @{pages=$doc.ComputeStatistics(2);words=$doc.ComputeStatistics(0);tables=$doc.Tables.Count;figures=$doc.InlineShapes.Count;toc=$doc.TablesOfContents.Count;tof=$doc.TablesOfFigures.Count;pdf=$Pdf}
    $resultado | ConvertTo-Json -Compress
} finally {
    if ($null -ne $doc) { $doc.Close(0) }
    if ($null -ne $word) { $word.Quit() }
    if ($null -ne $doc) { $null=[System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($doc) }
    if ($null -ne $word) { $null=[System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word) }
}
