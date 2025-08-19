# Análise de Qualidade de Código

Uma aplicação Vue.js para análise automática da qualidade de código em commits, fornecendo insights sobre boas práticas e sugestões de melhoria.

## 🚀 Funcionalidades

- **Análise Detalhada**: Avaliação completa de variáveis, funções, comentários e coesão de métodos
- **Sugestões Inteligentes**: Recomendações personalizadas para melhorar a qualidade do código
- **Múltiplas Linguagens**: Suporte para análise de código em diferentes linguagens de programação
- **Interface Moderna**: Design responsivo e intuitivo
- **Filtros Avançados**: Filtragem por qualidade de código

## 🛠️ Tecnologias Utilizadas

- **Vue 3** - Framework JavaScript progressivo
- **Vue Router** - Roteamento oficial para Vue.js
- **Pinia** - Gerenciamento de estado
- **Vite** - Build tool e dev server
- **CSS3** - Estilização moderna e responsiva

4. Acesse a aplicação em `http://localhost:3000`

## 🏗️ Estrutura do Projeto

```
src/
├── assets/          # Arquivos estáticos (CSS, imagens)
├── components/      # Componentes Vue reutilizáveis
├── models/          # Classes de modelo de dados
├── router/          # Configuração de rotas
├── stores/          # Stores do Pinia
├── views/           # Páginas/views da aplicação
├── App.vue          # Componente raiz
└── main.js          # Ponto de entrada
```

## 📋 Scripts Disponíveis

- `npm run dev` - Inicia o servidor de desenvolvimento
- `npm run build` - Gera build de produção
- `npm run preview` - Visualiza o build de produção
- `npm run lint` - Executa o linter

## 🎨 Componentes Principais

### CommitAnalysisCard
Exibe informações detalhadas sobre uma análise de commit, incluindo:
- Informações do commit (ID, autor, data)
- Score geral e qualidade
- Estatísticas (linhas adicionadas/removidas)
- Avaliação detalhada de código limpo
- Lista de arquivos analisados
- Sugestões de melhoria

### RatingChart
Componente para exibir avaliações de código limpo com:
- Barras de progresso coloridas
- Scores individuais para cada critério
- Justificativas para as avaliações

### FileList
Lista de arquivos analisados com:
- Informações do arquivo (nome, extensão)
- Estatísticas de linhas
- Avaliação individual do arquivo
- Sugestões específicas do arquivo

### SuggestionList
Lista de sugestões de melhoria com:
- Título e descrição da sugestão
- Badges de prioridade, tipo e dificuldade
- Referência ao arquivo
- Recursos de estudo

## 🔧 Configuração

### Variáveis de Ambiente
Crie um arquivo `.env` na raiz do projeto:

```env
VITE_API_URL=http://localhost:8080/api
```

### Personalização de Estilos
Os estilos podem ser personalizados editando:
- `src/assets/main.css` - Estilos globais
- Arquivos `.vue` individuais - Estilos específicos dos componentes

## 📱 Responsividade

A aplicação é totalmente responsiva e funciona em:
- Desktop (1200px+)
- Tablet (768px - 1199px)
- Mobile (< 768px)

## 🤝 Contribuição

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo `LICENSE` para mais detalhes.

