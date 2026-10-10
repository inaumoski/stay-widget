from pathlib import Path
import json, os, subprocess
root=Path(__file__).resolve().parents[1]
marker=root/'.local/staywidget-cluster.marker'
if not marker.is_file(): raise SystemExit('Run tools/dev-database.py first.')
password=json.loads((root/'.local/credentials.json').read_text())['password']
env=dict(os.environ,STAYWIDGET_TEST_ADMIN=f'Host=127.0.0.1;Port=55439;Database=postgres;Username=staywidget_dev;Password={password}', STAYWIDGET_ROOT=str(root))
dotnet=Path(os.environ['LOCALAPPDATA'])/'Fantoolie/dotnet/dotnet.exe'
raise SystemExit(subprocess.call([str(dotnet),'test','StayWidget.slnx','-c','Release'],cwd=root,env=env))
