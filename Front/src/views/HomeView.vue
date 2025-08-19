<template>
  <div class="home">
    <div class="hero-section">
      <h1>Análise de Qualidade de Código</h1>
      <p class="hero-description">
        Ferramenta para análise automática da qualidade de código em commits, 
        fornecendo insights sobre boas práticas e sugestões de melhoria.
      </p>
      <router-link to="/analyses" class="cta-button">
        Ver Análises
      </router-link>
    </div>

    <div class="features-section">
      <h2>Recursos</h2>
      <div class="features-grid">
        <div class="feature-card">
          <div class="feature-icon">📊</div>
          <h3>Análise Detalhada</h3>
          <p>Avaliação completa de variáveis, funções, comentários e coesão de métodos.</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">💡</div>
          <h3>Sugestões Inteligentes</h3>
          <p>Recomendações personalizadas para melhorar a qualidade do código.</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">🔧</div>
          <h3>Múltiplas Linguagens</h3>
          <p>Suporte para análise de código em diferentes linguagens de programação.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'HomeView'
}
</script>

<style scoped>
.home {
  max-width: 1200px;
  margin: 0 auto;
}

.hero-section {
  text-align: center;
  padding: 3rem 0;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 12px;
  margin-bottom: 3rem;
}

.hero-section h1 {
  font-size: 2.5rem;
  margin-bottom: 1rem;
}

.hero-description {
  font-size: 1.2rem;
  margin-bottom: 2rem;
  opacity: 0.9;
}

.cta-button {
  display: inline-block;
  background-color: #28a745;
  color: white;
  padding: 1rem 2rem;
  border-radius: 8px;
  text-decoration: none;
  font-weight: 600;
  transition: background-color 0.3s;
}

.cta-button:hover {
  background-color: #218838;
}

.features-section {
  padding: 2rem 0;
}

.features-section h2 {
  text-align: center;
  margin-bottom: 2rem;
  font-size: 2rem;
  color: #333;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 2rem;
}

.feature-card {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  box-shadow: 0 4px 6px rgba(0,0,0,0.1);
  text-align: center;
  transition: transform 0.3s;
}

.feature-card:hover {
  transform: translateY(-5px);
}

.feature-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.feature-card h3 {
  margin-bottom: 1rem;
  color: #333;
}

.feature-card p {
  color: #666;
  line-height: 1.6;
}
</style>
