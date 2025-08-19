<template>
  <div id="app">
    <header class="app-header">
      <h1>Análise de Qualidade de Código</h1>
      <nav>
        <router-link to="/" class="nav-link">Início</router-link>
        <router-link to="/analyses" class="nav-link">Análises</router-link>
      </nav>
    </header>

    <main class="app-main">
      <router-view />
    </main>

    <footer class="app-footer">
      <p>&copy; 2024 Análise de Commits</p>
    </footer>
  </div>
</template>

<script>
export default {
  name: 'App'
}
</script>

<style scoped>
.app-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.app-header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

nav {
  display: flex;
  gap: 1rem;
}

.nav-link {
  color: white;
  text-decoration: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  transition: background-color 0.3s;
}

.nav-link:hover {
  background-color: rgba(255,255,255,0.1);
}

.app-main {
  min-height: calc(100vh - 140px);
  padding: 2rem;
  background-color: #f8f9fa;
}

.app-footer {
  background-color: #343a40;
  color: white;
  text-align: center;
  padding: 1rem;
}
</style>
