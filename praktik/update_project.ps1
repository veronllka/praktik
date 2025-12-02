# Script to add TaskQRCodeWindow files to project
$csprojPath = "praktik.csproj"
$content = Get-Content $csprojPath -Raw

# Add TaskQRCodeWindow.xaml after QuickNoteWindow.xaml
$xamlPattern = '    </Page>\r\n    <Compile Include="App.xaml.cs">'
$xamlReplacement = @"
    </Page>
    <Page Include="TaskQRCodeWindow.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
    <Compile Include="App.xaml.cs">
"@
$content = $content -replace [regex]::Escape($xamlPattern), $xamlReplacement

# Add TaskQRCodeWindow.xaml.cs after QuickNoteWindow.xaml.cs
$csPattern = '    </Compile>\r\n    <Compile Include="Models\WorkPlannerContext.cs" />'
$csReplacement = @"
    </Compile>
    <Compile Include="TaskQRCodeWindow.xaml.cs">
      <DependentUpon>TaskQRCodeWindow.xaml</DependentUpon>
      <SubType>Code</SubType>
    </Compile>
    <Compile Include="Models\WorkPlannerContext.cs" />
"@
$content = $content -replace [regex]::Escape($csPattern), $csReplacement

#  Add QRCoder packages
$qrcoderPattern = '    <PackageReference Include="ControlzEx">'
$qrcoderReplacement = @"
    <PackageReference Include="QRCoder">
      <Version>1.7.0</Version>
    </PackageReference>
    <PackageReference Include="QRCoder.Xaml">
      <Version>1.7.0</Version>
    </PackageReference>
    <PackageReference Include="ControlzEx">
"@
$content = $content -replace [regex]::Escape($qrcoderPattern), $qrcoderReplacement

# Save the file
Set-Content $csprojPath -Value $content -NoNewline

Write-Host "Project file updated successfully"
