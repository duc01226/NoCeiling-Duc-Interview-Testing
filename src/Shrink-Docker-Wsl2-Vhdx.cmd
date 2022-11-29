wsl --shutdown
diskpart
# open window Diskpart
select vdisk file="%LocalAppData%\Docker\wsl\data\ext4.vhdx"
attach vdisk readonly
compact vdisk
detach vdisk
exit