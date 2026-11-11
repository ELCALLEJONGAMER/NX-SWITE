# Informe de actualización del catálogo (2026-11-11 22:06)

⚠️ 5 módulo(s) requieren atención · 51 sin cambios.

## Actualizado (5)

- **atmosphere_mod**: 1.12.1+ -> 1.12.2+ (estructura validada, firma SHA256 generada)

<details><summary>Estructura en la microSD</summary>

Cambios respecto a la versión anterior:
```diff
(misma estructura de archivos)
```
Contenido final en la microSD:
```
/atmosphere/config_templates/exosphere.ini
/atmosphere/config_templates/override_config.ini
/atmosphere/config_templates/stratosphere.ini
/atmosphere/config_templates/system_settings.ini
/atmosphere/hbl.nsp
/atmosphere/hbl_html/accessible-urls/accessible-urls.txt
/atmosphere/package3
/atmosphere/reboot_payload.bin
/atmosphere/stratosphere.romfs
/fusee.bin
/hbmenu.nro
/switch/daybreak.nro
/switch/haze.nro
/switch/reboot_to_payload.nro
```

</details>

- **firmware**: 23.0.1 -> 23.5.0 (estructura validada, firma SHA256 generada)

<details><summary>Estructura en la microSD</summary>

Cambios respecto a la versión anterior:
```diff
+ /23.0.1/02a6b2d4d75db3370f64a88855682bef.cnmt.nca
+ /23.0.1/068a915f2e8d9b5310bc11d62840166e.cnmt.nca
+ /23.0.1/0bd2490ae110b1d013dd73cb289bd1ce.nca
+ /23.0.1/10807cc70767137a5e26e277e430e28d.nca
+ /23.0.1/13e72855720adedf9ebdcf88a22f1e9d.nca
+ /23.0.1/150ad51cfa8848789713706a384b548d.nca
+ /23.0.1/16dc2da330952377f1352832606308d6.nca
+ /23.0.1/193bc2a10f95f7fa1f7497ba6e35dd8a.cnmt.nca
+ /23.0.1/19ec3c187597d26e205fd6171f7f0bc8.nca
+ /23.0.1/1a571d6b205f88708f9283f7cdf09816.nca
+ /23.0.1/268a19bf43f2973a4141bbbfb20460bf.nca
+ /23.0.1/299bcd1b11e8fbd8b2a784f5d2f17929.cnmt.nca
+ /23.0.1/2b6f6359b73f29ceca12756cb9663450.cnmt.nca
+ /23.0.1/2d18f84796fd870b68041c22f771bc33.cnmt.nca
+ /23.0.1/2f40a38aea3bb4426beb4cff2c190073.nca
+ /23.0.1/302e8f125a12b674eb701e925535e206.cnmt.nca
+ /23.0.1/31adc4a073028a322038765b781510e0.nca
+ /23.0.1/395e62389e258582f06ba8b387e9f800.cnmt.nca
+ /23.0.1/3e99f0d2744c10c34f6e0e4a669d3295.cnmt.nca
+ /23.0.1/3f74e002e766e452b230e561ada88af4.cnmt.nca
+ /23.0.1/4042009cb7119f218204cbcafddf89a4.cnmt.nca
+ /23.0.1/41aa25ea075597f3773010390eb40554.cnmt.nca
+ /23.0.1/483ec0c7f88cb167c045533ad60f94b6.nca
+ /23.0.1/51793527a6f60b09ca044d5196d3236f.nca
+ /23.0.1/55c9089d8910ac96a868dc81bd52bcac.nca
+ /23.0.1/57d1febb79dbf4cda37a01e4fbb18a51.cnmt.nca
+ /23.0.1/5825f332de9810a10138bc654d889a73.cnmt.nca
+ /23.0.1/5b503b724b2e9e23820735827969ac25.nca
+ /23.0.1/5dfa2d7f9ce1e6d7df9443d4028b1458.cnmt.nca
+ /23.0.1/61685e9d7698430619001f8e1c3b06f4.nca
+ /23.0.1/62544327b33a46cc21b0ebb6ab927382.cnmt.nca
+ /23.0.1/62615d577b50544fefead0d386459547.cnmt.nca
+ /23.0.1/6634e64a79d54e6569f4b600f96e8eac.nca
+ /23.0.1/68e0e3cdfca91e787d10b8332390a04b.nca
+ /23.0.1/72d63179bd632710b69edc86b7fae4fc.cnmt.nca
+ /23.0.1/76fb7e25aa5dc7358be0de7499d1f735.cnmt.nca
+ /23.0.1/820a534f1e15037628afd6e24b56a6a0.nca
+ /23.0.1/825ca085cffe8fc25b697b460ccfbb32.cnmt.nca
+ /23.0.1/87e98615e5907e7ac88f80536fd0b1cf.nca
+ /23.0.1/882887ab27c1d9afadefb3760d8b3357.nca
+ /23.0.1/8f33bc0529a5c4184cc4c02c94469666.cnmt.nca
+ /23.0.1/92baaf8d2b1ecf8eec3262c240a38c27.cnmt.nca
+ /23.0.1/99eda77f7bf243bdc137965cbd80b602.nca
+ /23.0.1/9ad91c364e0e11630922941cbc3defbb.nca
+ /23.0.1/9b5a2b33230fe3c9eb6e1ef598934096.nca
+ /23.0.1/a09e9b451b0fb1f0231397d34140f47e.cnmt.nca
+ /23.0.1/a59b200b8d2fa9f2064f2c8a28305dc4.cnmt.nca
+ /23.0.1/a6ee308fc8401a65a814e18488518f6c.cnmt.nca
+ /23.0.1/bd6c93e8ec529b515eb5cc38503dcc23.nca
+ /23.0.1/c3b62c78abbca08a444d451471b7898d.cnmt.nca
+ /23.0.1/d014d39bbef6146f536cecea42b062ca.nca
+ /23.0.1/d303c671991ee861783faf6898addfe6.cnmt.nca
+ /23.0.1/d6187cc0d147902843045c7772a131fd.cnmt.nca
+ /23.0.1/d8f51e62cff86186d9ab3a4f9c4921ad.nca
+ /23.0.1/da6ae627428f5fe36e26aafa833515a8.nca
+ /23.0.1/db554e9842313288a5b18f51c816e86d.cnmt.nca
+ /23.0.1/e2810411e2b04af67b99c0ea859f9dad.nca
+ /23.0.1/e43180618178feaab1d403ea2080e253.nca
+ /23.0.1/e8882faa4633ee08f7edf0b65a67f40b.nca
+ /23.0.1/f1eaafc03f5666c5349594531d7db399.cnmt.nca
+ /23.0.1/f1ec0219c175cc3d9b851a397e13f9cb.cnmt.nca
+ /23.0.1/f422ddead1fdfbc189d33b8d7dd046f8.cnmt.nca
+ /23.0.1/f58d34633f85c93ccf0eef4d28aa8038.nca
+ /23.0.1/f8489e2a9d97538116d76185a59e0cf0.nca
+ /23.0.1/fa402dbad86f1e30c43b10631f09aa7a.nca
+ /23.0.1/feff67c18c16d6ab46a42dc205a6b874.cnmt.nca
+ /23.0.1/ff0ae1d0707ab9a9b25f5d2ec4232099.cnmt.nca
- /23.0.1/00e57b67b4dbd750a2df0574cddc123d.nca
- /23.0.1/01b158b6de40fc9248e1e1767e231791.cnmt.nca
- /23.0.1/032049b76519faa2167703bf3eecadea.cnmt.nca
- /23.0.1/05b2410a157592b00710eba92a083f6f.cnmt.nca
- /23.0.1/0d2516ac51a57f1cb4e140810df18ba8.nca
- /23.0.1/0ece3a9ea789a97e689673f51daf3824.cnmt.nca
- /23.0.1/11402e85ea89281d7d069c73ee72a432.nca
- /23.0.1/11651d9dcdaba198324c8dac51a7091b.nca
- /23.0.1/124c2dd5087624175724f6cab1953504.cnmt.nca
- /23.0.1/16510dde912c6c6894d573f17006f113.nca
- /23.0.1/1eed56785513b285251a392addff4d63.nca
- /23.0.1/21604255efadc69a5380cbbf1d456182.cnmt.nca
- /23.0.1/2493cd2bf020b1db1a0b78da4412bab0.cnmt.nca
- /23.0.1/2720ee18cf5d303fce78f7fd3f2c1012.nca
- /23.0.1/2c25fe552e39617d5027f55653a50617.nca
- /23.0.1/3468522a925b2d99cfc559b263f54104.nca
- /23.0.1/41f679ddf5559bcba7374e2e7b4a75c0.cnmt.nca
- /23.0.1/43ddef5986fa9d2d08d9ec6eb1daa63f.cnmt.nca
- /23.0.1/44f0b66dbfee0c586d6612bd8bd73e4a.cnmt.nca
- /23.0.1/4c9cf6825c1861680abed1fdace7eecf.nca
- /23.0.1/4d9e756a5b78b8cc15c016712d874cba.nca
- /23.0.1/4f5245e13ee6c59ace64eed024dee7de.cnmt.nca
- /23.0.1/50966f0dff6cc599ca03328d81420587.nca
- /23.0.1/52a88b09d15a179cb0664a0ead2fb3ce.cnmt.nca
- /23.0.1/5695a29a203c35b8bfe9d1e2cfe35634.cnmt.nca
- /23.0.1/57033daed64dc454cab1ca99a96ba898.cnmt.nca
- /23.0.1/5bc39286ff59b4bd3abc2bbca467e86c.cnmt.nca
- /23.0.1/5c913d514bfbaec42614447f96d096ee.cnmt.nca
- /23.0.1/64fb27057bc5e2a72c8641ffb698bc09.nca
- /23.0.1/65e2fda359b7c51846eb55ca6c9667e3.nca
- /23.0.1/6c9e6a19c7d6a8410c477e303fbc97ba.nca
- /23.0.1/6de90f809f3e8687e2e7440ff2a3cf87.cnmt.nca
- /23.0.1/6f854c29f1200ff26072eca259ce47ce.cnmt.nca
- /23.0.1/7174ef97c2ebcedfb20430e127c61c74.cnmt.nca
- /23.0.1/72cf755559afee4849b048e302ddec7a.cnmt.nca
- /23.0.1/7416ea749f88bd27afa925f0049991e0.nca
- /23.0.1/755bcf33085ed666cbbcf34de83d1cb0.nca
- /23.0.1/75f0fa393686752f7c9ef4bec76360f0.nca
- /23.0.1/76115bfcaf2fccf8d33fe2f60e1fc098.nca
- /23.0.1/794c70265088a38f454d785642d84fcd.cnmt.nca
- /23.0.1/7dff571931a4de56a10f78699a9bf4ff.nca
- /23.0.1/7f186be8a6315af1b6259a891a84c3f9.cnmt.nca
- /23.0.1/812766b37251077267837ade6524073d.cnmt.nca
- /23.0.1/81c915cbb4f1fec21926da5ab4fee88d.cnmt.nca
- /23.0.1/830da1300a46d0ede11f0c89a82039bd.nca
- /23.0.1/868db676bbb1ba6a2452cd71332f7c3c.nca
- /23.0.1/91eb0083333c98ae19d3f3b3bcd3624f.nca
- /23.0.1/a0e910d601787b0f2e63a036fbd7955a.nca
- /23.0.1/a239ac30de3af6c4a5593a978873490c.cnmt.nca
- /23.0.1/ac77e53496a65d65d37e0aacaeb89e90.cnmt.nca
- /23.0.1/b698a9533b0b84a775b48f53badc35c5.nca
- /23.0.1/b9c84e2ce228d42d9be019e3bbdda10a.cnmt.nca
- /23.0.1/c119c6d39a859929bb112746e80e4b66.cnmt.nca
- /23.0.1/c1cad975a7fe0a2eb363410723d967e4.nca
- /23.0.1/ca1d2d911ed3d23b27a5e30e924aed5c.nca
- /23.0.1/ce3762365b288c07d135e95cb42d7027.cnmt.nca
- /23.0.1/ce6ae881c11498b00aac3307f2be3b65.nca
- /23.0.1/d3162ff52f608a70ddeb81b8b8341254.nca
- /23.0.1/d34b587ccfd475188c4a3116187ab46d.cnmt.nca
- /23.0.1/d6915f712aaa209c883a941741769fb8.cnmt.nca
- /23.0.1/d8dcad3c1b0fa45839f0eecfbff777ab.nca
- /23.0.1/dceb80c097b184b32c20a7c984d1b1b7.cnmt.nca
- /23.0.1/df356275b46189cdc296e3820b88b808.nca
- /23.0.1/e0810e933ada950db7c13eb3343a6651.cnmt.nca
- /23.0.1/e3b0b627e811a1031a92fc15354de853.nca
- /23.0.1/ecc04171b2ad9f2038a3ce1372318808.nca
- /23.0.1/fe6397f765496a3a19f41d41cfef56ce.cnmt.nca
```
Contenido final en la microSD:
```
/23.0.1/00e7f53ae3ee4d6fd915b1ae26fbd1be.cnmt.nca
/23.0.1/015531c2279f834e48500eb0c2a5f8c1.cnmt.nca
/23.0.1/015da7d509f1505f44e65240d22216be.cnmt.nca
/23.0.1/022e881c913a5c904d4652a5b71b8425.cnmt.nca
/23.0.1/02a6b2d4d75db3370f64a88855682bef.cnmt.nca
/23.0.1/02d227be9dd2682bc52551bd10cbe992.nca
/23.0.1/03ec860c6ac6c17f5d90562700e30e7f.nca
/23.0.1/04feb0345448f313167d6c454809f844.cnmt.nca
/23.0.1/0671113d5748913b84d94449dbfba4a1.cnmt.nca
/23.0.1/068050547d51676e43f1dfbdb241c0c1.cnmt.nca
/23.0.1/068a915f2e8d9b5310bc11d62840166e.cnmt.nca
/23.0.1/06b33165bad446736a8f8918f9b678e5.nca
/23.0.1/0bb5d66fd0fc610f4c3d70d6b8f69633.cnmt.nca
/23.0.1/0bd2490ae110b1d013dd73cb289bd1ce.nca
/23.0.1/0cf95a44b4b0b70c6d37477909f3c529.cnmt.nca
/23.0.1/0d6522396b8721c643c1d15e56438912.cnmt.nca
/23.0.1/0e9c81ff8b533a3cfebed187ad29ea1b.cnmt.nca
/23.0.1/0f4193e43f59a7ac33aada3ef4576625.nca
/23.0.1/0fcc9176e439cc5c1ac1cb09966d6795.nca
/23.0.1/10807cc70767137a5e26e277e430e28d.nca
/23.0.1/10c233403c349ee0098d8a59cdf5e329.nca
/23.0.1/1200b746290a1da555debfe56f194f25.nca
/23.0.1/12c4e06b758101746ca0063080a7a884.cnmt.nca
/23.0.1/13e72855720adedf9ebdcf88a22f1e9d.nca
/23.0.1/148526d4623146e0d1a4ca1c797eddac.nca
/23.0.1/14e378a8b3b2374e3fbeb60185d56457.cnmt.nca
/23.0.1/150ad51cfa8848789713706a384b548d.nca
/23.0.1/16dc2da330952377f1352832606308d6.nca
/23.0.1/17c96417dc19fc10592629c6b700731e.cnmt.nca
/23.0.1/183166177cd0bf2a516c61a2e3293238.nca
/23.0.1/193bc2a10f95f7fa1f7497ba6e35dd8a.cnmt.nca
/23.0.1/19ec3c187597d26e205fd6171f7f0bc8.nca
/23.0.1/1a571d6b205f88708f9283f7cdf09816.nca
/23.0.1/1ae33903fe0eb09b74615fb40f8bfead.cnmt.nca
/23.0.1/1c9af21fb8557f2fac1300ecb9012b48.nca
/23.0.1/1f1b160db410505a362eb52beadb74e3.cnmt.nca
/23.0.1/1f2cfea82e1cc1f46eb651c9ca20534f.nca
/23.0.1/20c2c361c675b3aa70f55e5b8a4e0bf5.nca
/23.0.1/21c1c05236c1a4bfe3a3cf093101c3c1.cnmt.nca
/23.0.1/23cce2ea69e2bd896aed33508ac2c2d9.nca
/23.0.1/23f87c289c6c5db25d2fdf2268a0baa6.cnmt.nca
/23.0.1/24c7e5954a97619c970ee0d6bc639931.nca
/23.0.1/262a579f6ebf4681ebda1010a46299a1.nca
/23.0.1/268a19bf43f2973a4141bbbfb20460bf.nca
/23.0.1/28b834870c5f9a87344d182b4914f03d.nca
/23.0.1/299bcd1b11e8fbd8b2a784f5d2f17929.cnmt.nca
/23.0.1/2ae702fda04da8c5b230708c45b300c8.cnmt.nca
/23.0.1/2b6f6359b73f29ceca12756cb9663450.cnmt.nca
/23.0.1/2d18f84796fd870b68041c22f771bc33.cnmt.nca
/23.0.1/2e0ec33c7059fea7780badcb852ca90a.cnmt.nca
/23.0.1/2f40a38aea3bb4426beb4cff2c190073.nca
/23.0.1/2ffc30ddfdc436c7d8fe2244a985ec1f.nca
/23.0.1/302e8f125a12b674eb701e925535e206.cnmt.nca
/23.0.1/31adc4a073028a322038765b781510e0.nca
/23.0.1/334d7a40bd0b18bc363c966e19f8bf1b.nca
/23.0.1/335fa63864b1b487fac5a911a4793d80.nca
/23.0.1/35861e61742f448461ccaf675587a9b0.cnmt.nca
/23.0.1/361bac511dc03a8f2b9ededee68d8d54.cnmt.nca
/23.0.1/38ac7257e3e822af9d4eb6f22d778826.cnmt.nca
/23.0.1/390a1e56f44cdf3ef6724fd01ea2f41c.cnmt.nca
/23.0.1/395e62389e258582f06ba8b387e9f800.cnmt.nca
/23.0.1/39d58df75af1e2a7b5c744ad0a1240fe.nca
/23.0.1/3aa8d007e9076c0f79433e0a98179e6c.cnmt.nca
/23.0.1/3d0575bbace6767a74ddf6a578baa5da.nca
/23.0.1/3e99f0d2744c10c34f6e0e4a669d3295.cnmt.nca
/23.0.1/3f74e002e766e452b230e561ada88af4.cnmt.nca
/23.0.1/4042009cb7119f218204cbcafddf89a4.cnmt.nca
/23.0.1/4121a8a4c1aefbe65aec5e2f37135221.nca
/23.0.1/41aa25ea075597f3773010390eb40554.cnmt.nca
/23.0.1/4297729dfb8bd65f592aa0db83121ff2.cnmt.nca
/23.0.1/42e8c03ecfed689dedd9c091b9f7c253.cnmt.nca
/23.0.1/44e2f6c262ba7b1bc7b2a516aecadf3c.cnmt.nca
/23.0.1/46e669b141a77159a8748f6da8894d0b.nca
/23.0.1/476497ba3b813fb4118893700cbe8f2c.nca
/23.0.1/481143a8fbe97368cbcec06b9e25bcf7.cnmt.nca
/23.0.1/483ec0c7f88cb167c045533ad60f94b6.nca
/23.0.1/498d9e585c6ea8e39707e868fda74579.cnmt.nca
/23.0.1/4cbc2f75b9e56983a87770c7be5c63cc.nca
/23.0.1/4d9776c9950ee88ad8d81c4484ab5a2f.nca
/23.0.1/516d6d6ff620f24b839451015e3f278c.nca
/23.0.1/51793527a6f60b09ca044d5196d3236f.nca
/23.0.1/5500fa7e04f1509f163c0fa57b898ddb.nca
/23.0.1/558d28de8fad94ed979634d5b0fc3ff5.nca
/23.0.1/55c9089d8910ac96a868dc81bd52bcac.nca
/23.0.1/57d1febb79dbf4cda37a01e4fbb18a51.cnmt.nca
/23.0.1/5811004c289ec1fb5e9ce9dff63d9ad3.cnmt.nca
/23.0.1/5825f332de9810a10138bc654d889a73.cnmt.nca
/23.0.1/5a37bf120bfe194f1987ccef5e5f752b.nca
/23.0.1/5b503b724b2e9e23820735827969ac25.nca
/23.0.1/5b6f69833be3e6442255a869b89f918c.cnmt.nca
/23.0.1/5dfa2d7f9ce1e6d7df9443d4028b1458.cnmt.nca
/23.0.1/5f1a820d85afe34cb98e92a5c1a2cc7f.cnmt.nca
/23.0.1/5fc07a396cdc90a1e172efb90ed56786.nca
/23.0.1/5fe581c21696d6fc143702918a103709.nca
/23.0.1/609324dfdb9b5b664ef990ce63853305.nca
/23.0.1/61685e9d7698430619001f8e1c3b06f4.nca
/23.0.1/616e071c161b1eda9bbde3db2fbf7bb7.nca
/23.0.1/617a3300548a555c7679d197b0cea17d.cnmt.nca
/23.0.1/62544327b33a46cc21b0ebb6ab927382.cnmt.nca
/23.0.1/62615d577b50544fefead0d386459547.cnmt.nca
/23.0.1/64d321af6c61633963053fc08121e6f3.nca
/23.0.1/65aad8545e58b565f568931c4153d28a.cnmt.nca
/23.0.1/6634e64a79d54e6569f4b600f96e8eac.nca
/23.0.1/67254473fc56e791b106359434d0efdd.nca
/23.0.1/680a190f692308df6ad8aed6307c7b7f.cnmt.nca
/23.0.1/68e0e3cdfca91e787d10b8332390a04b.nca
/23.0.1/69079efd1620d1f75a6ef9847956cd3a.nca
/23.0.1/6b2d68265bbe184fdc395d1a0c042dfa.nca
/23.0.1/6de442d643dd607684903fbd22e8e552.nca
/23.0.1/6de732b9ac29c0bf17bb019896370c94.cnmt.nca
/23.0.1/6f481f3e8f863b5895701517ff810662.nca
/23.0.1/7010af4f35a1d3c5d31b9c45aa67b2b7.nca
/23.0.1/70ca668de1b12e6ea525148f09ff422b.nca
/23.0.1/713301ec1e1e6ab18e4587faac0a816e.cnmt.nca
/23.0.1/718f52a943afdf011700b29d04d76937.nca
/23.0.1/725c03555e53ad1f72ac98807f6826d9.nca
/23.0.1/72d63179bd632710b69edc86b7fae4fc.cnmt.nca
/23.0.1/744471ddf47fc51b06291043283adca4.nca
/23.0.1/74cbc0b6973b6feb803d9dc685853d1e.cnmt.nca
/23.0.1/751aaa3f586c8e866175af588aebc26e.nca
/23.0.1/7596489c598365b6ac5acc9c0696a6f3.nca
/23.0.1/75a0e8144d97cf3dc9d05643f7221fb5.cnmt.nca
/23.0.1/76fb7e25aa5dc7358be0de7499d1f735.cnmt.nca
/23.0.1/78fc86e5a2f4d6930f593f91bf77f66c.cnmt.nca
/23.0.1/79d742794129d2c7ebfc63174c155bf5.cnmt.nca
/23.0.1/7aa27a2dbbc67fd4641ba7869bcbfbc5.nca
/23.0.1/7b7d558cbdb954036174f45b4461cf45.cnmt.nca
/23.0.1/7c6a7f1874ecff80d73d67a39e6042bc.cnmt.nca
/23.0.1/81af198323a2680f10f6f45d0b310169.cnmt.nca
/23.0.1/820a534f1e15037628afd6e24b56a6a0.nca
/23.0.1/825ca085cffe8fc25b697b460ccfbb32.cnmt.nca
/23.0.1/82fc5766da4eeb58801d4e919b346982.nca
/23.0.1/84e3c723c13a36002a7c5192af9886b0.nca
/23.0.1/85dcf709e0a8db1627739663da3c658f.nca
/23.0.1/8632fdf5cdff1995273e35ef59271836.cnmt.nca
/23.0.1/87daeece6f5c71d682d4d57fa633ce8f.cnmt.nca
/23.0.1/87e98615e5907e7ac88f80536fd0b1cf.nca
/23.0.1/882887ab27c1d9afadefb3760d8b3357.nca
/23.0.1/88e3aa4713e1329930b648d80b1ae56e.cnmt.nca
/23.0.1/897ebd2460c9fbd446d2df08cbf62abb.cnmt.nca
/23.0.1/8f33bc0529a5c4184cc4c02c94469666.cnmt.nca
/23.0.1/910f249247bfc69d957b72a7ca54ed44.nca
/23.0.1/91e93b1037aeee6236fa189820775dc4.nca
/23.0.1/92baaf8d2b1ecf8eec3262c240a38c27.cnmt.nca
/23.0.1/94068dbd6eb42b8a235e56e8b364d367.cnmt.nca
/23.0.1/951af6ccc1c8346d799295f78e7b1b2b.nca
/23.0.1/9532ee7bca1f4d2ec5c90e315ec820c7.nca
/23.0.1/9548dbfcbaf1c195c0976ce2a164c374.nca
/23.0.1/956070ca7ff88336f055abc9bc33dc22.cnmt.nca
/23.0.1/99eda77f7bf243bdc137965cbd80b602.nca
/23.0.1/9a678cd06e635d8e5a307a961f85107b.cnmt.nca
/23.0.1/9ad91c364e0e11630922941cbc3defbb.nca
/23.0.1/9ae96224f286b5f739456ed22b2f649d.cnmt.nca
/23.0.1/9b5a2b33230fe3c9eb6e1ef598934096.nca
/23.0.1/9bf6022014166897b511a815783e1ae0.cnmt.nca
/23.0.1/9d7556bb740ee7a90e972a8416e25f10.nca
/23.0.1/9dd102458c789990eb6903b9ed5fb574.cnmt.nca
/23.0.1/9f0888d4b0ec90243f7b695bf9eeb4e8.cnmt.nca
/23.0.1/a09e9b451b0fb1f0231397d34140f47e.cnmt.nca
/23.0.1/a1239c7d50d406a44dd83201d970513e.nca
/23.0.1/a2dc9f03ae32440a016ec2a550cff8ce.cnmt.nca
/23.0.1/a30d19a0fce5fac66117bca8955683ed.nca
/23.0.1/a59b200b8d2fa9f2064f2c8a28305dc4.cnmt.nca
/23.0.1/a62108db6726a3d1b0a21718af5d10a4.nca
/23.0.1/a6ee308fc8401a65a814e18488518f6c.cnmt.nca
/23.0.1/a96e8996c41c05e3199dcfdadc99fe34.cnmt.nca
/23.0.1/a9d3dfa376905271081adbabb4257a0d.nca
/23.0.1/aa96bf0ee712d2c8a1797789fec9788d.cnmt.nca
/23.0.1/aa9a47bbb58a33731a3b92bc27fcaf9c.nca
/23.0.1/afc78068f5165699a8c9db2bef43dd1d.nca
/23.0.1/b02b17cc4aee38151e5a9bb4b0d969c4.nca
/23.0.1/b1e08d9bf9541fb6fdf58c7ba32fb2b3.nca
/23.0.1/b4d0f5c2f2aa92e4e7305c58c9aa028c.cnmt.nca
/23.0.1/b837233731cc2c6b3bc384489be7d46a.nca
/23.0.1/b9152056012c0d292641980788413803.cnmt.nca
/23.0.1/bd6c93e8ec529b515eb5cc38503dcc23.nca
/23.0.1/be3340e963424d805c8310b06e56c457.cnmt.nca
/23.0.1/bfab8caa278e456f9fc5e1f5fd734feb.nca
/23.0.1/c270ae1edf573a0f5126d524c795c194.cnmt.nca
/23.0.1/c3b62c78abbca08a444d451471b7898d.cnmt.nca
/23.0.1/c58381611258f72aa5d28a55a338eee6.cnmt.nca
/23.0.1/c5d032046782d4bd6fc4b5c012a4e110.nca
/23.0.1/c662174a4e815559883c7b8559565b10.cnmt.nca
/23.0.1/c76e989ee4451e15c11538585ddf2449.nca
/23.0.1/ca6e167d711b924ba111ecb24f6a4ae0.nca
/23.0.1/cb52f5f38ddd5088de28af57a555ccef.nca
/23.0.1/cc84f42746477d2c47a1a25241413a06.nca
/23.0.1/cd6d6e96f36f7cf99f85ddae181c9354.cnmt.nca
/23.0.1/ce8da28da36c34eaf8fc96c647e9f20f.cnmt.nca
/23.0.1/ce9c00ed1eaccaf7ea3d7b12201496c2.cnmt.nca
/23.0.1/cea18f43e45e0a123fa7e0353c49fd59.nca
/23.0.1/d014d39bbef6146f536cecea42b062ca.nca
/23.0.1/d159d0cb394d810f1ab5fd8a4134428f.cnmt.nca
/23.0.1/d25523bba5c868ce619a7cbf03cea48d.cnmt.nca
/23.0.1/d303c671991ee861783faf6898addfe6.cnmt.nca
/23.0.1/d43802ae368a813b7f94d4c142ed5785.nca
/23.0.1/d57037150094dd02694e8b9906c7d67d.cnmt.nca
/23.0.1/d6187cc0d147902843045c7772a131fd.cnmt.nca
/23.0.1/d6dce3a8bd63b2c182b9991fdf40fa6e.nca
/23.0.1/d6e5634ac6eccf56691c301140075bda.cnmt.nca
/23.0.1/d6e7f11f3e0239f759c2b1f4869d4e49.nca
/23.0.1/d873a3dea728a4fbb380d5428e7147f6.nca
/23.0.1/d8f51e62cff86186d9ab3a4f9c4921ad.nca
/23.0.1/da6ae627428f5fe36e26aafa833515a8.nca
/23.0.1/da78855be31285e66b2f16e0dd7606f8.cnmt.nca
/23.0.1/db554e9842313288a5b18f51c816e86d.cnmt.nca
/23.0.1/dc46b03817d78b7d9fbd82ea59533501.cnmt.nca
/23.0.1/dc52422918276f38dad48e3ff2ff9c28.nca
/23.0.1/dcaa83353918ca082038b1e793630d4e.cnmt.nca
/23.0.1/dcd587b5ace675d71181fd8be52ba616.cnmt.nca
/23.0.1/de06479df858e376df0d2388e73d97c4.cnmt.nca
/23.0.1/e1d532a45a06cdf09e522366de77e2ea.cnmt.nca
/23.0.1/e2810411e2b04af67b99c0ea859f9dad.nca
/23.0.1/e3820683d2dd1957c133ade91be3613f.nca
/23.0.1/e43180618178feaab1d403ea2080e253.nca
/23.0.1/e45f3f977092659c79e69562cd029efd.nca
/23.0.1/e481ece5d922f9a3776493f891fa0ef9.cnmt.nca
/23.0.1/e8882faa4633ee08f7edf0b65a67f40b.nca
/23.0.1/e88f318ed500d87379380d6cafa1ddbe.cnmt.nca
/23.0.1/e88fb69dfbd4e8caefa663a4490766a4.cnmt.nca
/23.0.1/ea8593a2d495ee66b6f49ea48c5ad259.cnmt.nca
/23.0.1/ece1d88b4660c817d56183a515372a48.cnmt.nca
/23.0.1/f1eaafc03f5666c5349594531d7db399.cnmt.nca
/23.0.1/f1ec0219c175cc3d9b851a397e13f9cb.cnmt.nca
/23.0.1/f422ddead1fdfbc189d33b8d7dd046f8.cnmt.nca
/23.0.1/f4abad8232f8f23c34016fdd7f890132.cnmt.nca
/23.0.1/f5718a5ad728bb81fe664e11aa41090e.cnmt.nca
/23.0.1/f58d34633f85c93ccf0eef4d28aa8038.nca
/23.0.1/f7f085ef0f48225e75ebfac689d68158.cnmt.nca
/23.0.1/f810431b7aadaeb135c378e499f0873d.nca
/23.0.1/f8489e2a9d97538116d76185a59e0cf0.nca
/23.0.1/f89ba1c512389dd5ffa9b95fa83264cf.cnmt.nca
/23.0.1/fa402dbad86f1e30c43b10631f09aa7a.nca
/23.0.1/fad78a11d5713a9ad7dd76c99f0aaaae.nca
/23.0.1/fbf0305a6f45a2005cfa2fafb92814ae.nca
/23.0.1/fe9bef6a51181d77b816adc7ac11135b.nca
/23.0.1/feff67c18c16d6ab46a42dc205a6b874.cnmt.nca
/23.0.1/ff0ae1d0707ab9a9b25f5d2ec4232099.cnmt.nca
```

</details>

- **hekate**: 6.5.4 -> 6.5.5 (estructura validada, firma SHA256 generada)

<details><summary>Estructura en la microSD</summary>

Cambios respecto a la versión anterior:
```diff
(misma estructura de archivos)
```
Contenido final en la microSD:
```
/bootloader/res/icon_payload.bmp
/bootloader/res/icon_switch.bmp
/bootloader/sys/emummc.kipm
/bootloader/sys/l4t/bpmpfw.bin
/bootloader/sys/l4t/bpmpfw_b01.bin
/bootloader/sys/l4t/mtc_tbl_b01.bin
/bootloader/sys/l4t/sc7entry.bin
/bootloader/sys/l4t/sc7exit.bin
/bootloader/sys/l4t/sc7exit_b01.bin
/bootloader/sys/libsys_lp0.bso
/bootloader/sys/libsys_minerva.bso
/bootloader/sys/nyx.bin
/bootloader/sys/res.pak
/bootloader/sys/thk.bin
/bootloader/update.bin
/payload.bin
```

</details>

- **ultrahand**: 2.5.3 -> 2.5.4 (estructura validada, firma SHA256 generada)

<details><summary>Estructura en la microSD</summary>

Cambios respecto a la versión anterior:
```diff
(misma estructura de archivos)
```
Contenido final en la microSD:
```
/atmosphere/contents/420000000007E51A/exefs.nsp
/atmosphere/contents/420000000007E51A/flags/boot2.flag
/atmosphere/contents/420000000007E51A/toolbox.json
/atmosphere/contents/420000000007E51B/exefs.nsp
/atmosphere/exefs_patches/audio_mastervolume/3a75b4a62dad189b2c8b30e4dedb4e15ece56850.ips
/atmosphere/exefs_patches/audio_mastervolume/62b74bae7163b0ea9b29ed5a050c09cd8299c14e.ips
/atmosphere/exefs_patches/audio_mastervolume/d1e98d469d5bd0e878fdb06e7ca517ba411d4682.ips
/atmosphere/exefs_patches/audio_mastervolume/e163d0b718e32f87430ca8c98fda2e22ba733990.ips
/config/ultrahand/.sounds/default.zip
/config/ultrahand/assets/ppkantorski-1.rgba
/config/ultrahand/assets/ppkantorski-2.rgba
/config/ultrahand/flags/NTP_SYNC_PENDING.flag
/config/ultrahand/lang/de.json
/config/ultrahand/lang/en.json
/config/ultrahand/lang/es.json
/config/ultrahand/lang/fr.json
/config/ultrahand/lang/it.json
/config/ultrahand/lang/ja.json
/config/ultrahand/lang/ko.json
/config/ultrahand/lang/nl.json
/config/ultrahand/lang/pl.json
/config/ultrahand/lang/pt.json
/config/ultrahand/lang/ru.json
/config/ultrahand/lang/uk.json
/config/ultrahand/lang/zh-cn.json
/config/ultrahand/lang/zh-tw.json
/config/ultrahand/payloads/ultrahand_updater.bin
/config/ultrahand/sounds/enter.wav
/config/ultrahand/sounds/exit.wav
/config/ultrahand/sounds/move.wav
/config/ultrahand/sounds/notification.wav
/config/ultrahand/sounds/off.wav
/config/ultrahand/sounds/on.wav
/config/ultrahand/sounds/settings.wav
/config/ultrahand/sounds/tick.wav
/config/ultrahand/sounds/wall.wav
/config/ultrahand/themes/classic.ini
/config/ultrahand/themes/ultra-blue.ini
/config/ultrahand/themes/ultra.ini
/config/ultrahand/wallpapers/atmosphere.rgba
/switch/.overlays/ovlmenu.ovl
/switch/Ultrahand-Reload/Ultrahand-Reload.nro
```

</details>

- **missioncontrol**: 0.16.0 -> 0.16.1 (estructura validada, firma SHA256 generada)

<details><summary>Estructura en la microSD</summary>

Cambios respecto a la versión anterior:
```diff
+ /atmosphere/exefs_patches/hid_patches/D209357BF939276179EF943C8DC88E482C7F588E.ips
- /atmosphere/exefs_patches/hid_patches/12DB6C7DF6BF40BC02695C611EC46F458504A5AD.ips
- /atmosphere/exefs_patches/hid_patches/403978CD2BC64E4ACB3BF1A5E556F582BFC3498B.ips
- /atmosphere/exefs_patches/hid_patches/8BDBD223E61D122E98CDBFC77C231BBB5CBE56E3.ips
```
Contenido final en la microSD:
```
/atmosphere/contents/010000000000bd00/exefs.nsp
/atmosphere/contents/010000000000bd00/flags/boot2.flag
/atmosphere/contents/010000000000bd00/mitm.lst
/atmosphere/contents/010000000000bd00/toolbox.json
/atmosphere/exefs_patches/bluetooth_patches/064521C83399E09105D9A190688BF8B8C2C7F17C.ips
/atmosphere/exefs_patches/bluetooth_patches/08601CC4C629F981F92CE3A53C2633FB46DDEA00.ips
/atmosphere/exefs_patches/bluetooth_patches/144B678306269E3D5245497373C09866D2BB8997.ips
/atmosphere/exefs_patches/bluetooth_patches/1494B3B0E7AA4234ED20F6A5EEA60620522C68DD.ips
/atmosphere/exefs_patches/bluetooth_patches/1F66DEC82762261430EE292541FB42F5C4E10AA5.ips
/atmosphere/exefs_patches/bluetooth_patches/2C0A323887EF342C448A72DDCC4CD737A298CD46.ips
/atmosphere/exefs_patches/bluetooth_patches/2CAAD3898B7B2F01DF66480EDD919C6A26735EC9.ips
/atmosphere/exefs_patches/bluetooth_patches/2FB16085E21E54B90E88ED2F98CCC850334E128E.ips
/atmosphere/exefs_patches/bluetooth_patches/3714A43B8F9608D08E15067AB1D9CE8E68FE56BC.ips
/atmosphere/exefs_patches/bluetooth_patches/4F57845D1DC166A46C42D5AF45164196DC7CD198.ips
/atmosphere/exefs_patches/bluetooth_patches/532B667232B3992A0A3D007AA85E4944C4138D15.ips
/atmosphere/exefs_patches/bluetooth_patches/63C4312A5D74B29DFC3DA25B6E3E4825575D23C9.ips
/atmosphere/exefs_patches/bluetooth_patches/68C915388537833D1AC9F3920708A1A0FF73E35A.ips
/atmosphere/exefs_patches/bluetooth_patches/6B33C2B7C850A47325F775218C40FCEA2F935A65.ips
/atmosphere/exefs_patches/bluetooth_patches/6FCF8BF53C10AA346555D74EC602FE53BF3BD88E.ips
/atmosphere/exefs_patches/bluetooth_patches/874B8CD62BF6715648382BBADB70790558FB3F35.ips
/atmosphere/exefs_patches/bluetooth_patches/8C2A437B9890F6DA0FC3B17442B10E7C6FCDA6AA.ips
/atmosphere/exefs_patches/bluetooth_patches/935E9D4099CE74FC162915E7CA4952E64FAD6671.ips
/atmosphere/exefs_patches/bluetooth_patches/9480B34033289645E7924E99F467CCB9B4632A5D.ips
/atmosphere/exefs_patches/bluetooth_patches/9E95C91568327AD850AC0C2246F1078DE72B28F9.ips
/atmosphere/exefs_patches/bluetooth_patches/A3278455B3EC297F0CE6CD629183EC86376EE0DC.ips
/atmosphere/exefs_patches/bluetooth_patches/A5E6C2D20334CC787617A15BFB9F53F2DABA3E41.ips
/atmosphere/exefs_patches/bluetooth_patches/B58A6445D7C724D6285EB3922FE6BBAACDA33EB3.ips
/atmosphere/exefs_patches/bluetooth_patches/BF43446888D1182FF37420B6762EB1136DE45947.ips
/atmosphere/exefs_patches/bluetooth_patches/C91C6FC8AA4C39222D6CCFE0FFF468543105E59B.ips
/atmosphere/exefs_patches/bluetooth_patches/CCE16D29FE9BCB44AC40BB1703D586FE467496CE.ips
/atmosphere/exefs_patches/bluetooth_patches/CF0C34FC10F75EB2DAFB302FAA3E0C188621B2AD.ips
/atmosphere/exefs_patches/bluetooth_patches/DBCA0074043CA7E74E9C58A84ACD7A3058968528.ips
/atmosphere/exefs_patches/bluetooth_patches/DE1AC63A4A0B77CCEA606CF15B62AA587EB9CACC.ips
/atmosphere/exefs_patches/bluetooth_patches/E9A6DBF13FE4A6833606A834C7BA1B44F4DDD60F.ips
/atmosphere/exefs_patches/bluetooth_patches/F013FF4FC88A8376A6F3C401485C0718E1B5E148.ips
/atmosphere/exefs_patches/bluetooth_patches/F3DCA5480F0523699683C0D16B7C51C6394897AF.ips
/atmosphere/exefs_patches/bluetooth_patches/FEE47E024FC9C62838FF1A476DE243DCE673DA7E.ips
/atmosphere/exefs_patches/btm_patches/0A2E87DB96AC17AC6260A878D215A20D9EDF0917.ips
/atmosphere/exefs_patches/btm_patches/14F4527DDB0C367055C384E2DAD037B3996664FE.ips
/atmosphere/exefs_patches/btm_patches/18D29F36EFBD14C6187F1E337749326DBC6BB205.ips
/atmosphere/exefs_patches/btm_patches/1D460F761919B94522A10B3EB98F4EC75FBDBC4F.ips
/atmosphere/exefs_patches/btm_patches/29B9122754BCEE48E4225933979F233939A49CF2.ips
/atmosphere/exefs_patches/btm_patches/344A9B35C77BF85392AFE04B38701D1EB0BD41CF.ips
/atmosphere/exefs_patches/btm_patches/37B45CA18B0D5E1768119971677690857A185AAB.ips
/atmosphere/exefs_patches/btm_patches/38FD02642C00EB3EDD3625728EA308C3B3CDF4B4.ips
/atmosphere/exefs_patches/btm_patches/3B03D2FF7700627216F3562340991844C957F457.ips
/atmosphere/exefs_patches/btm_patches/479F51DAD54016A7F68CFCB521F5D3E37B2855B2.ips
/atmosphere/exefs_patches/btm_patches/4B92BF76660CCD6DE63528EFDE37147BA5997B09.ips
/atmosphere/exefs_patches/btm_patches/4BBB2B9942C1DA04BBFB4933AA2E350BB88F3C14.ips
/atmosphere/exefs_patches/btm_patches/52E5D19BDDF49586901BA4CF869723BCA222AAEC.ips
/atmosphere/exefs_patches/btm_patches/58518E8DDEB26A678AD332B3C8D772C24F2B0636.ips
/atmosphere/exefs_patches/btm_patches/5A2AA468F272E49EBF0FAB8B379CC5B32C1A7409.ips
/atmosphere/exefs_patches/btm_patches/5B1ED394A7DD3910E5B375F3C90B13505347AB0A.ips
/atmosphere/exefs_patches/btm_patches/5C60D97B9800074FE755847EE2380477A751BDAE.ips
/atmosphere/exefs_patches/btm_patches/65AFEE4DCE1AB795540D204AF5D20AAF83C68A82.ips
/atmosphere/exefs_patches/btm_patches/6DC55293131192B617564C62732CC2CF445BD248.ips
/atmosphere/exefs_patches/btm_patches/702DADCB98BF9D965F570BCB6A6480C00AC536FB.ips
/atmosphere/exefs_patches/btm_patches/72FC67F6B4096661FDF4773527E84967B7D8A460.ips
/atmosphere/exefs_patches/btm_patches/A0542C24E727081A6A63208700C9CE2952B6914C.ips
/atmosphere/exefs_patches/btm_patches/A27AE05114E0A17160A8C74BE8472F8EE6ABB273.ips
/atmosphere/exefs_patches/btm_patches/ADB4B59EFF92BC8D10F28F87FD9C13666213EE53.ips
/atmosphere/exefs_patches/btm_patches/B517E72EBBE1EB0223C3DF26E0017F727F9AD776.ips
/atmosphere/exefs_patches/btm_patches/BEB8B07DA484E04B326001799DBE53D53D0B5D52.ips
/atmosphere/exefs_patches/btm_patches/C35EF7937BD7C91FDC66F2E983D39388988364AF.ips
/atmosphere/exefs_patches/btm_patches/F4D4976A1E1E6F498F6824151C11C6AA9EF81375.ips
/atmosphere/exefs_patches/btm_patches/F5FC951E3AB69B6FBE6250B7B73D22B3F75EDE28.ips
/atmosphere/exefs_patches/btm_patches/F9A63D3E811CAE0F06495124042CE7F951CE505B.ips
/atmosphere/exefs_patches/btm_patches/FE1CFC6CE0B8F5A3C779ED67C9EF8C08DE959AE9.ips
/atmosphere/exefs_patches/hid_patches/0ABCF878B3E22287ABD15F1F73B365DB26A4E124.ips
/atmosphere/exefs_patches/hid_patches/0F02EFFAEF77015948F635C1559FC32507074451.ips
/atmosphere/exefs_patches/hid_patches/1780B569DE805B4E9AFBCC366D93BB313DCBA67E.ips
/atmosphere/exefs_patches/hid_patches/296FB63B038723955A5EEE4720C3E17BF49B5ECC.ips
/atmosphere/exefs_patches/hid_patches/2ABBA006E9E162C1EF0A28E6477725F2836FFEC9.ips
/atmosphere/exefs_patches/hid_patches/453AF9911A69ED566732A7AF2BF54B0B50BC3255.ips
/atmosphere/exefs_patches/hid_patches/47BCA8366FC3D4C9E86592E8FC42485989E69CCB.ips
/atmosphere/exefs_patches/hid_patches/6AC68185D4597477D9AD8A2EBE014733F2BCB104.ips
/atmosphere/exefs_patches/hid_patches/74480E653551A0415F90CD61E4B851C60E71E674.ips
/atmosphere/exefs_patches/hid_patches/765B710D7D40049E06C1326F5B57123B74A2E4AD.ips
/atmosphere/exefs_patches/hid_patches/98EB0277ADFC171F79765FCC8A56674AE39B08FC.ips
/atmosphere/exefs_patches/hid_patches/9A1F471859D99957A81B2FC21169EE4DBC191734.ips
/atmosphere/exefs_patches/hid_patches/A2F71DB9568A864B8AC81AC79E2735A53A9CD291.ips
/atmosphere/exefs_patches/hid_patches/ACA9C5D9BF1A0558251E6D2FE2D0CA4DFE7B90AC.ips
/atmosphere/exefs_patches/hid_patches/AE8B325431ED958F335615EBA5312870A92978CB.ips
/atmosphere/exefs_patches/hid_patches/B5D1C5A111EB89E72F2EBF93D1343DFD7BF4E421.ips
/atmosphere/exefs_patches/hid_patches/BBEE6C2C132C849BC313B0E38732D91B9C45FBFD.ips
/atmosphere/exefs_patches/hid_patches/C3030310E47B3841417518902AFFB2301F0033FC.ips
/atmosphere/exefs_patches/hid_patches/C5FCB68A301FD160FF5209C54175AC51D69750BC.ips
/atmosphere/exefs_patches/hid_patches/C9636704F7726BF77CEBBC53FFB14A80D8F789A1.ips
/atmosphere/exefs_patches/hid_patches/CA1A12452AF8789F5D03BF2A70260CE80D7DA5C4.ips
/atmosphere/exefs_patches/hid_patches/CEE923637C0797454FBE98A64F63D2B9F39B62FC.ips
/atmosphere/exefs_patches/hid_patches/CFCBD39A38FC2E8FC1273B4B5BF7C619FA93579E.ips
/atmosphere/exefs_patches/hid_patches/D209357BF939276179EF943C8DC88E482C7F588E.ips
/atmosphere/exefs_patches/hid_patches/D7E71B92FAFD5E6D670F36593D98DC30AD15A24F.ips
/atmosphere/exefs_patches/hid_patches/E5139E8D47BE57C31DA97FCFBB66472416D60BD1.ips
/atmosphere/exefs_patches/hid_patches/FA528960A4ED9B4C408D2667BFE7C8BCD80E1FE2.ips
/config/MissionControl/missioncontrol.ini.template
```

</details>


