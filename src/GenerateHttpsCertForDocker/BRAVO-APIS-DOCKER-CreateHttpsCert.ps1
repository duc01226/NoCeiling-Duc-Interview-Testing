$Title = "BRAVO TALENTS APIS DOCKER Create Https Cert"

# create a SAN cert for both host.docker.internal and localhost
$cert = New-SelfSignedCertificate -DnsName "host.docker.internal", "localhost", "account-api" -CertStoreLocation cert:\localmachine\my
Export-Certificate -FilePath $env:userprofile\.aspnet\https\aspnetapp_ca_cert.crt -Cert $cert

#export it for docker container to pick up later
$password = ConvertTo-SecureString -String "123456" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $env:userprofile\.aspnet\https\aspnetapp.pfx -Password $password

# trust it on your host machine
$storeRoot = new-object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    "localmachine"
)
$storeRoot.Open("ReadWrite")
$storeRoot.Add($cert)
$storeRoot.Close()

# trust it on your host machine
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store "TrustedPublisher","LocalMachine"
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()
