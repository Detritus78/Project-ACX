import re, glob, json, subprocess, sys
src = '/home/jj/Work/ac/project-acx/Source/ACE.Server/Factories'
enum = {}
for m in re.finditer(r'^\s*(\w+)\s*=\s*(\d+),', open(src + '/Enum/WeenieClassName.cs').read(), re.M):
    enum[m.group(1)] = int(m.group(2))
names = set()
files = [f for f in glob.glob(src + '/Tables/Wcids/Weapons/*.cs') if 'Caster' not in f]
for f in files:
    for m in re.finditer(r'WeenieClassName\.(\w+)', open(f).read()):
        names.add(m.group(1))
ids = sorted(enum[n] for n in names if n in enum and n != 'undef')
missing = [n for n in names if n not in enum]
print('names', len(names), 'ids', len(ids), 'missing', missing, file=sys.stderr)
q = f"""select w.class_Id, w.class_Name, s.value,
 (select value from weenie_properties_d_i_d where object_Id=w.class_Id and type=1),
 coalesce((select value from weenie_properties_int where object_Id=w.class_Id and type=45),0),
 coalesce((select value from weenie_properties_int where object_Id=w.class_Id and type=3),0),
 coalesce((select value from weenie_properties_int where object_Id=w.class_Id and type=48),0),
 coalesce((select value from weenie_properties_int where object_Id=w.class_Id and type=18),0),
 coalesce((select value from weenie_properties_int where object_Id=w.class_Id and type=1),0),
 coalesce((select value from weenie_properties_d_i_d where object_Id=w.class_Id and type=7),0)
 from weenie w join weenie_properties_string s on s.object_Id=w.class_Id and s.type=1 where w.class_Id in ({','.join(map(str, ids))});"""
out = subprocess.run(['docker','exec','-i','acx-preview-db-1','sh','-c','exec mysql -N -uroot -p"$MYSQL_ROOT_PASSWORD" ace_world'], input=q, capture_output=True, text=True).stdout
rows = []
for line in out.strip().split('\n'):
    c = line.split('\t')
    rows.append(dict(wcid=int(c[0]), cls=c[1], name=c[2], setup=int(c[3]), dmg=int(c[4]), palette=int(c[5]), skill=int(c[6]), ui=int(c[7]), itemtype=int(c[8]), clothing=int(c[9])))
json.dump(rows, open('weapons.json','w'), indent=0)
print('rows', len(rows), file=sys.stderr)
