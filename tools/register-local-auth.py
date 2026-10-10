"""Add a distinct local client without printing or copying existing credentials."""
from pathlib import Path
import json, os, secrets
root = Path(__file__).resolve().parents[1]
auth = Path(os.environ['APPDATA']) / 'Microsoft/UserSecrets/fantoolie-auth-development-2026/secrets.json'
settings = root / 'src/StayWidget.Web/appsettings.Local.json'
if not auth.is_file() or not settings.is_file():
    raise SystemExit('Prepare local DB and the existing issuer configuration first.')
data = json.loads(auth.read_text(encoding='utf-8-sig'))
client_id = 'staywidget-local'
indices = {int(k.split(':')[2]) for k in data if k.startswith('Auth:Clients:') and k.split(':')[2].isdigit()}
existing = next((i for i in indices if data.get(f'Auth:Clients:{i}:ClientId') == client_id), None)
index = existing if existing is not None else (max(indices, default=-1)+1)
secret = data.get(f'Auth:Clients:{index}:ClientSecret') if existing is not None else secrets.token_hex(32)
if not secret:
    raise SystemExit('Existing StayWidget client has no secret; refusing to overwrite.')
fields={'ClientId':client_id,'ClientSecret':secret,'DisplayName':'StayWidget local','RedirectUri':'https://localhost:7259/signin-funtoolio','PostLogoutRedirectUri':'https://localhost:7259/signout-callback-oidc','Theme':'light'}
for k,v in fields.items(): data[f'Auth:Clients:{index}:{k}']=v
temporary = auth.with_suffix('.staywidget.tmp')
temporary.write_text(json.dumps(data,indent=2),encoding='utf-8')
temporary.replace(auth)
cfg=json.loads(settings.read_text(encoding='utf-8'))
cfg['Authentication']={'Enabled':True,'Issuer':'https://localhost:7443/','ClientId':client_id,'ClientSecret':secret}
settings.write_text(json.dumps(cfg,indent=2),encoding='utf-8')
print('Distinct StayWidget local client configured. Issuer startup must apply its registration; no other client changed.')
