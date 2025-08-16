# Configuração de GPU para RefactorScore

Este guia explica como configurar o RefactorScore para usar aceleração GPU com o Ollama.

## Pré-requisitos

1. **GPU NVIDIA compatível**
   - Mínimo recomendado: GTX 1060 ou superior com 4GB+ VRAM
   - Para modelos maiores: RTX 2060 ou superior com 6GB+ VRAM

2. **Drivers NVIDIA atualizados**
   - Baixe de: https://www.nvidia.com.br/Download/index.aspx
   - Instale seguindo as instruções

3. **CUDA Toolkit**
   - Baixe de: https://developer.nvidia.com/cuda-downloads
   - Escolha Windows > x86_64 > 10 > exe (local)

## Instalação do Docker com suporte a GPU

### 1. Instale o Docker Desktop para Windows
   - Baixe de: https://www.docker.com/products/docker-desktop
   - Durante a instalação, certifique-se de marcar a opção "Use WSL 2 instead of Hyper-V"

### 2. Instale o WSL 2 (Windows Subsystem for Linux)
   ```powershell
   wsl --install
   ```

### 3. Configure o Docker para usar GPU
   - Abra o Docker Desktop
   - Vá para Settings > Resources > WSL Integration
   - Habilite "Enable integration with additional distros"
   - Selecione sua distribuição Linux

### 4. Instale o NVIDIA Container Toolkit no WSL
   - Abra um terminal WSL
   - Execute:
   ```bash
   # Configure o repositório
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
   curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
     sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
     sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   
   # Atualize e instale
   sudo apt-get update
   sudo apt-get install -y nvidia-container-toolkit
   
   # Configure o runtime
   sudo nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

## Verificação da Instalação

1. **Teste se a GPU está disponível para o Docker**
   ```bash
   docker run --rm --gpus all nvidia/cuda:11.0-base nvidia-smi
   ```

2. **Verifique se o Ollama está usando a GPU**
   ```bash
   docker exec refactorscore-ollama nvidia-smi
   ```

## Uso com RefactorScore

1. **Inicie os serviços com suporte a GPU**
   ```bash
   docker-compose up -d
   ```

2. **Verifique o status do modelo**
   ```bash
   curl http://localhost:11434/api/tags
   ```

3. **Monitore o uso da GPU durante a execução**
   ```bash
   docker exec refactorscore-ollama nvidia-smi
   ```

## Solução de Problemas

- **Erro "could not select device driver with capabilities: [[gpu]]"**
  - Verifique se o NVIDIA Container Toolkit está instalado corretamente
  - Reinicie o Docker Desktop

- **GPU não aparece no nvidia-smi**
  - Verifique se os drivers NVIDIA estão instalados corretamente
  - Execute `nvidia-smi` diretamente no Windows para confirmar

- **Modelo muito lento mesmo com GPU**
  - Verifique o uso da GPU com `nvidia-smi`
  - Considere usar modelos quantizados (q4_0, q5_K_M) que são otimizados para GPU 