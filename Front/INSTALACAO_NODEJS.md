# Instalação do Node.js no Windows

## 🚨 Problema Identificado

O erro que você está enfrentando indica que o **Node.js e npm não estão instalados** no seu sistema. O erro `ENOENT: no such file or directory` acontece porque o npm não consegue encontrar o arquivo `package.json` no diretório correto.

## 📥 Como Instalar o Node.js

### Opção 1: Download Direto (Recomendado)

1. **Acesse o site oficial do Node.js:**
   - Vá para: https://nodejs.org/

2. **Baixe a versão LTS (Long Term Support):**
   - Clique no botão verde "LTS" (versão recomendada)
   - Isso baixará o instalador `.msi` para Windows

3. **Execute o instalador:**
   - Dê duplo clique no arquivo baixado
   - Siga as instruções do assistente de instalação
   - **Importante:** Mantenha todas as opções padrão marcadas

4. **Verifique a instalação:**
   - Feche e reabra o PowerShell/Terminal
   - Execute os comandos:
   ```bash
   node --version
   npm --version
   ```

### Opção 2: Usando Chocolatey (Se já tiver instalado)

Se você já tem o Chocolatey instalado:
```bash
choco install nodejs
```

### Opção 3: Usando Winget (Windows 10/11)

```bash
winget install OpenJS.NodeJS
```

## 🔧 Após a Instalação

1. **Reinicie o terminal/PowerShell**
2. **Navegue até o diretório do projeto:**
   ```bash
   cd C:\TCC\Front
   ```

3. **Verifique se está no diretório correto:**
   ```bash
   dir
   ```
   Você deve ver o arquivo `package.json` na lista.

4. **Instale as dependências:**
   ```bash
   npm install
   ```

5. **Execute o projeto:**
   ```bash
   npm run dev
   ```

## 🛠️ Solução de Problemas

### Erro: "npm não é reconhecido"
- **Causa:** Node.js não está no PATH do sistema
- **Solução:** Reinstale o Node.js e certifique-se de marcar a opção "Add to PATH"

### Erro: "Access Denied"
- **Causa:** Permissões insuficientes
- **Solução:** Execute o PowerShell como Administrador

### Erro: "Porta já em uso"
- **Causa:** Outro processo está usando a porta 3000
- **Solução:** O Vite automaticamente tentará a próxima porta disponível

## 📋 Verificação da Instalação

Após instalar, execute estes comandos para verificar:

```bash
# Verificar versão do Node.js
node --version

# Verificar versão do npm
npm --version

# Verificar diretório atual
pwd

# Listar arquivos do projeto
ls
```

## 🎯 Próximos Passos

1. **Instale o Node.js** seguindo as instruções acima
2. **Reinicie o terminal**
3. **Navegue para o diretório do projeto:**
   ```bash
   cd C:\TCC\Front
   ```
4. **Execute:**
   ```bash
   npm install
   npm run dev
   ```

## 📞 Ainda com Problemas?

Se ainda enfrentar problemas após seguir estas instruções:

1. **Verifique se o Node.js foi instalado corretamente:**
   - Abra o Painel de Controle > Programas > Programas e Recursos
   - Procure por "Node.js" na lista

2. **Verifique as variáveis de ambiente:**
   - Pressione `Win + R`, digite `sysdm.cpl`
   - Vá para "Avançado" > "Variáveis de Ambiente"
   - Verifique se o caminho do Node.js está em "PATH"

3. **Reinstale o Node.js:**
   - Desinstale completamente o Node.js
   - Baixe a versão mais recente do site oficial
   - Reinstale seguindo as instruções

## 🔗 Links Úteis

- **Site oficial do Node.js:** https://nodejs.org/
- **Documentação do npm:** https://docs.npmjs.com/
- **Documentação do Vite:** https://vitejs.dev/
