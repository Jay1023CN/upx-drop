# UPX Drop

Windows ԭ�� EXE / DLL ��קѹ�����ߡ�C# + WPF������δ���޸ĵĹٷ� UPX������ Node.js��Python �����ԱȨ�ޡ�

## ʹ��

�� `UpxDrop.exe`������ EXE / DLL ���ļ��У��Զ��Ŷ�ѹ����Ĭ�Ͼ��� `-7`��ֱ�Ӹ���ԭ�ļ���**�����ɱ���**��֧�ֿ��� `-1`��������� `--best` �ͽ�ѹ `-d`���ɹر��Զ���ʼ�����û����´�����������

�״�ʹ�õ�������عٷ����桱���� UPX �ٷ� GitHub Release ���ع̶��汾 5.2.1 ���˶� SHA-256��Ҳ���Ե����ѡ�� UPX��ʹ�������еĹٷ� `upx.exe`��������ɺ��������ʹ�á�����ֻ����������ʱ���������ϴ��������ļ���

�������̣�ͬĿ¼��ʱ���� �� �ٷ� UPX �� `upx -t` У��ѹ����� �� �˶�ԭ�ļ�δ�仯 �� ԭ���滻���������ɰ汾��û��ѹ������ʱ����ԭ�ļ���ʧ�ܡ�ֹͣʱ������ʱ�ļ����ϵ��ǿ�ƽ������̿������� `.upxdrop-*` ��ʱ�ļ�������ֹͣ���᳷����ǰ�Ѿ��ɹ����ļ���

�ļ��л�ݹ�ɨ�裬�ų�Ŀ¼���ӣ�������� 5000 ���ļ���֧�����ĺͿո�·����ѹ����ĳ����Կ�ֱ�����У������ʽ�������� UPX ��������ǩ���ļ��������������ƻ�ǩ������ʹ�� `--force`��UPX У�鲻����Ӧ�ù��ܲ��ԣ���������ǰ��������������֤��

## ���������

Windows 10 / 11��.NET Framework 4.8��ϵͳ�Դ��������������� SDK �� NuGet ������

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

��� `dist/UpxDrop/UpxDrop.exe`��`tests/CoreTests.cs` �ñ��� fixture �ӽ�����֤���ǡ�ȡ����ʧ�ܱ���ԭ�ļ���ǩ�������������޸ļ���·�����Σ�fixture ���Բ�����ٷ� UPX ����ʵ��������֤��

```powershell
# ���ز�У��ٷ�����Ͷ�Ӧ����������Դ��鵵
powershell -ExecutionPolicy Bypass -File .\download-upx.ps1
# ����������������ı�Я�汾
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

## ��Դ����

Copyright (C) 2026 Jay1023CN and contributors.

UPX Drop is free software, licensed under **GPL-2.0-or-later**. See [LICENSE](LICENSE). It is distributed without warranty.

UPX �����ṩ������ԭ�������У���� [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) �� `tools/UPX-LICENSE`��UPX �Բ���δ���޸ĵĹٷ������ stub ѹ���ĳ����ṩ�������⣬�����ַ�������ҵ�������ڵ�ѹ�������������������������Լ���������ⲻ����������·ַ� UPX ����ʱ�� GPL Դ�����������

��������ʱͬʱ�ṩ����������Դ�롢�����ű���LICENSE �͵������������������������ UPX ���棬Ӧ��ͬһ����λ���ṩ `upx-5.2.1-src.tar.xz` ����Դ��鵵�͹ٷ������ļ������ؽű��Ѿ�׼�������̡�Ĭ�������汾���û��������عٷ����档

����Ŀ�Ƕ���ͼ��ǰ�ˣ��� UPX �ٷ���������ϵ��
