"""Create/start only this repository's marked loopback development cluster."""
from pathlib import Path
import json, os, secrets, subprocess, sys

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local'
DATA = LOCAL / 'pgdata'
BIN = Path(os.environ.get('STAYWIDGET_PG_BIN', ROOT.parent / 'shop-organizer/.local/pgsql/bin'))
PORT = 55439
LOCAL.mkdir(exist_ok=True)
marker = LOCAL / 'staywidget-cluster.marker'
if DATA.exists() and not marker.exists():
    raise SystemExit('Refusing an unmarked database directory.')
password_path = LOCAL / 'credentials.json'
if not password_path.exists():
    password_path.write_text(json.dumps({'password': secrets.token_hex(24)}), encoding='utf-8')
password = json.loads(password_path.read_text(encoding='utf-8'))['password']
env = dict(os.environ, PGPASSWORD=password)
def pg(exe, *args, check=True):
    return subprocess.run([str(BIN / (exe + '.exe')), *args], env=env, check=check,
                          creationflags=subprocess.CREATE_NO_WINDOW)
if not DATA.exists():
    pw = LOCAL / 'init-password.tmp'
    pw.write_text(password, encoding='utf-8')
    try:
        pg('initdb', '-D', str(DATA), '-U', 'staywidget_dev', '--pwfile='+str(pw), '--auth=scram-sha-256', '--encoding=UTF8', '--locale=C')
        marker.write_text('StayWidget disposable local cluster on 127.0.0.1:55439\n', encoding='utf-8')
        with (DATA / 'postgresql.conf').open('a', encoding='utf-8') as f:
            f.write("\nlisten_addresses='127.0.0.1'\nport=55439\nshared_buffers='32MB'\nmax_connections=30\n")
    finally:
        pw.unlink(missing_ok=True)
if pg('pg_ctl', '-D', str(DATA), 'status', check=False).returncode:
    pg('pg_ctl', '-D', str(DATA), '-l', str(LOCAL / 'postgres.log'), '-w', 'start')
exists = subprocess.run([str(BIN / 'psql.exe'), '-h','127.0.0.1','-p',str(PORT),'-U','staywidget_dev','-d','postgres','-Atc',"SELECT 1 FROM pg_database WHERE datname='staywidget_dev'"],env=env,check=True,capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
if exists.stdout.strip() != '1':
    pg('createdb','-h','127.0.0.1','-p',str(PORT),'-U','staywidget_dev','staywidget_dev')
settings = ROOT / 'src/StayWidget.Web/appsettings.Local.json'
cfg = json.loads(settings.read_text(encoding='utf-8')) if settings.exists() else {}
cfg['ConnectionStrings'] = {'StayWidget': f'Host=127.0.0.1;Port={PORT};Database=staywidget_dev;Username=staywidget_dev;Password={password};Include Error Detail=false'}
cfg.setdefault('StayWidget', {}).update({'DevelopmentLogin': True, 'DataRoot': str(LOCAL / 'keys')})
settings.write_text(json.dumps(cfg, indent=2), encoding='utf-8')
print('StayWidget-only cluster ready. Private settings written; no migration applied.')
