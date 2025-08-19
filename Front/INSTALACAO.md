# Instruções de Instalação

## Pré-requisitos

Antes de executar este projeto, você precisa ter instalado:

### 1. Node.js e npm
- Baixe e instale o Node.js em: https://nodejs.org/
- Escolha a versão LTS (Long Term Support)
- O npm será instalado automaticamente com o Node.js

### 2. Verificar a instalação
Após a instalação, abra um terminal e execute:
```bash
node --version
npm --version
```

## Instalação do Projeto

1. **Clone o repositório** (se ainda não fez):
```bash
git clone <url-do-repositorio>
cd Front
```

2. **Instale as dependências**:
```bash
npm install
```

3. **Execute o servidor de desenvolvimento**:
```bash
npm run dev
```

4. **Acesse a aplicação**:
Abra seu navegador e acesse: `http://localhost:3000`

## Scripts Disponíveis

- `npm run dev` - Inicia o servidor de desenvolvimento
- `npm run build` - Gera build de produção
- `npm run preview` - Visualiza o build de produção
- `npm run lint` - Executa o linter

## Estrutura do Projeto

```
Front/
├── src/
│   ├── assets/          # CSS e arquivos estáticos
│   ├── components/      # Componentes Vue
│   ├── models/          # Classes de modelo
│   ├── router/          # Configuração de rotas
│   ├── stores/          # Stores do Pinia
│   ├── utils/           # Funções utilitárias
│   ├── views/           # Páginas/views
│   ├── App.vue          # Componente raiz
│   └── main.js          # Ponto de entrada
├── dados.json           # Dados de exemplo
├── index.html           # Template HTML
├── package.json         # Dependências e scripts
├── vite.config.js       # Configuração do Vite
└── README.md            # Documentação
```

## Solução de Problemas

### Erro: "npm não é reconhecido"
- Verifique se o Node.js está instalado corretamente
- Reinicie o terminal após a instalação
- Verifique se o Node.js está no PATH do sistema

### Erro: "Cannot find module"
- Execute `npm install` novamente
- Delete a pasta `node_modules` e execute `npm install`

### Erro de porta em uso
- O Vite usa a porta 3000 por padrão
- Se estiver ocupada, o Vite tentará a próxima porta disponível
- Verifique a mensagem no terminal para a porta correta

## Desenvolvimento

### Adicionando novas dependências
```bash
npm install nome-do-pacote
```

### Adicionando dependências de desenvolvimento
```bash
npm install --save-dev nome-do-pacote
```

### Atualizando dependências
```bash
npm update
```

## Build de Produção

Para gerar uma versão otimizada para produção:

```bash
npm run build
```

Os arquivos serão gerados na pasta `dist/`.

## Deploy

Após executar `npm run build`, você pode fazer deploy dos arquivos da pasta `dist/` em qualquer servidor web estático.
