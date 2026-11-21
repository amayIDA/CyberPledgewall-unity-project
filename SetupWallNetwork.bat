@echo off
netsh advfirewall firewall add rule name="Cyber Pledge Wall" dir=in action=allow protocol=UDP localport=7777,7778 profile=any
