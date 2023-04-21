"PLEASE INSTALL OPENSSL ON YOUR COMPUTER BEFORE RUNNING THIS SCRIPT"

$certPass="123456"
$certSubj="host.docker.internal"
$certAltNames = "DNS:localhost,DNS:host.docker.internal,DNS:account-api" # i believe you can also add individual IP addresses here like so: IP:127.0.0.1
$opensslPath="C:\Program Files\OpenSSL-Win64\bin\openssl.exe" #assuming you can download OpenSSL, I believe no installation is necessary
$certPathFolder="$env:userprofile\.aspnet\https"
$caPfxPath="$certPathFolder\aspnetapp.pfx"
$caCertPath="$certPathFolder\cert\aspnetapp_ca_cert.crt"
$caCertPrivateKeyPath="$certPathFolder\cert\aspnetapp_ca_cert.key"

$hasCreatedCaCert = Test-Path -Path $caCertPath -PathType Leaf

New-Item -ItemType Directory -Force -Path $certPathFolder
New-Item -ItemType Directory -Force -Path "$certPathFolder\cert"

#generate a self-signed cert with multiple domains
Start-Process -NoNewWindow -Wait -FilePath $opensslPath -ArgumentList "req -x509 -nodes -newkey rsa:2048 -keyout ",
$caCertPrivateKeyPath,
"-out", $caCertPath,
"-subj `"/CN=$certSubj`" -addext `"subjectAltName=$certAltNames`""

# this time round we convert PEM format into PKCS#12 (aka PFX) so .net core app picks it up
Start-Process -NoNewWindow -Wait -FilePath $opensslPath -ArgumentList "pkcs12 -export -in ",
$caCertPath,
"-inkey ", $caCertPrivateKeyPath,
"-out ", $caPfxPath,
"-passout pass:$certPass"

$password = ConvertTo-SecureString -String $certPass -Force -AsPlainText

"Type Password $certPass"
$cert = Get-PfxCertificate -FilePath $caPfxPath

# trust it on your host machine
$storeRoot = new-object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    "localmachine"
)
$storeRoot.Open("ReadWrite")
$storeRoot.Add($cert)
$storeRoot.Close()

"Local Self-Signed Cert for Docker Created"
