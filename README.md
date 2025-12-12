# P01-IA

# Documentação do projeto final da UC Inteligência Artificial Aplicada a Jogos

Esta documentação detalha a implementação da IA no projeto, abordando as tecnologias utilizadas, como estão implementadas e exemplos de funcionamento. O objetivo é fornecer uma referência completa para entendimento e futuras modificações.

Este trabalho foi realizado por:
- 27945 Paulo Bastos    
- 27947 Bruno Mesquita  
- 27935 José Lima       

## Tecnologias de IA Utilizadas

No projeto, a IA dos bots utiliza três tecnologias principais da lista fornecida:

1. **State Machines (Máquinas de Estados)**
2. **Pathfinding (NavMesh / Caminho em Grafos)**
3. **Heurísticas e Pontos de Interesse (como parte da decisão de combate e pickups)**

Estas tecnologias são combinadas para criar um comportamento realista de bots em patrulha, combate e procura de recursos.

## 1. State Machines (Máquinas de Estados)

### Conceito

Uma máquina de estados (State Machine) define um conjunto de estados possíveis para um agente e as transições entre esses estados com base em condições específicas. É uma das formas mais comuns de organizar o comportamento de NPCs.

### Implementação

* O script **BotAI_Proto** define uma enumeração `BotState` com os seguintes estados:

  * `Patrol`: Patrulha por waypoints predefinidos.
  * `Chase`: Persegue o jogador ao ser detetado.
  * `Attack`: Ataca o jogador quando está na distância ideal.
  * `Search`: Procura pelo último local conhecido do jogador.
  * `Retreat`: Recua para recuperar vida.
  * `GoToAmmo`: Procura por munição quando está baixa.

* Cada estado tem uma função correspondente `Tick<State>` que define o comportamento naquele estado.

#### Exemplo de uso de estados

```csharp
switch (currentState)
{
    case BotState.Patrol: TickPatrol(); break;
    case BotState.Chase: TickChase(); break;
    case BotState.Attack: TickAttack(); break;
    case BotState.Search: TickSearch(); break;
    case BotState.Retreat: TickRetreat(); break;
    case BotState.GoToAmmo: TickGoToAmmo(); break;
}
```

* A transição de estados é feita através da função `ChangeState(BotState newState)`, que considera fatores como saúde, munição, visibilidade do jogador e distância.

### Benefícios

* Estrutura clara e modular do comportamento do bot.
* Facilita a adição de novos estados ou alteração de regras de transição.
* Permite que múltiplos bots partilhem a mesma lógica de estados.

## 2. Pathfinding (NavMesh / Grafos)

### Conceito

Pathfinding é a capacidade de encontrar um caminho ótimo ou viável de um ponto a outro em um ambiente, evitando obstáculos. No Unity, a tecnologia principal utilizada é o **NavMesh**.

### Implementação

* O script **BotAI_Proto** utiliza `NavMeshAgent` para movimentação.
* Pontos de patrulha (`patrolPoints`) e posições de pickups são definidos como `Transform[]`.
* A movimentação entre pontos é feita com `agent.SetDestination(destination)`.

#### Algoritmo de pathfinding usado

* O Unity calcula automaticamente o caminho mais curto dentro do NavMesh usando o algoritmo A* internamente.
* O bot também evita colisões com obstáculos usando `NavMeshObstacle` e o sistema de colisão integrado.

#### Exemplo de movimentação de patrulha

```csharp
void TickPatrol()
{
    agent.isStopped = false;
    agent.speed = baseSpeed;
    agent.SetDestination(patrolPoints[patrolIndex].position);
}
```

* Para fugir ou procurar pickups, é usada a mesma abordagem, apenas alterando o destino dinamicamente.
* Função utilitária `GetClosestTransform` seleciona o waypoint ou pickup mais próximo.

### Benefícios

* Movimentação realista sem colisões.
* Suporta múltiplos bots em simultâneo.
* Integra-se com a lógica de estado, permitindo transições suaves entre patrulha, perseguição e fuga.

---

## 3. Heurísticas e Pontos de Interesse (Decisão de Combate)

### Conceito

Heurísticas são regras ou métricas utilizadas para tomar decisões rápidas com base em parâmetros do ambiente ou do estado do agente.

No projeto, heurísticas são usadas para:

* Determinar quando o bot deve atacar, perseguir, procurar ou recuar.
* Selecionar o pickup mais próximo de vida ou munição.
* Avaliar a distância ideal para combate.

### Implementação

* A função `Update()` do **BotAI_Proto** calcula condições como:

  * `lowHealth`: se a vida está abaixo do limite definido.
  * `lowAmmo`: se a munição está baixa.
  * `distToPlayer`: distância até o jogador.
  * `playerVisible`: se o jogador está visível após um Raycast.

* Dependendo destas variáveis, o bot escolhe o estado apropriado:

```csharp
if (lowHealth) ChangeState(BotState.Retreat);
else if (lowAmmo) ChangeState(BotState.GoToAmmo);
else if (playerVisible)
    ChangeState(distToPlayer <= idealCombatDistance ? BotState.Attack : BotState.Chase);
else
    ChangeState(BotState.Patrol);
```

* Para prever o movimento do jogador e melhorar a precisão do tiro, o **BotCombat** usa uma heurística de lead:

```csharp
Vector3 futurePos = currentTarget.position + targetVelocity * timeToHit * leadAccuracy;
```

* O erro de mira é aplicado através de uma curva de dispersão (`spreadOverDistance`) multiplicada por um fator de dificuldade (`aimInaccuracyMultiplier`).

### Benefícios

* Bots reagem dinamicamente a mudanças no ambiente.
* Permite comportamentos realistas sem necessidade de cálculos complexos de IA.
* Torna o combate mais desafiador e natural.

## Estrutura de Scripts Relacionados

* **BotAI_Proto.cs**: Core da IA, gerencia estados, percepção, decisões e movimentação.
* **BotCombat.cs**: Gerencia ataques, previsão de tiros, armas e recarga.
* **BOTDeath.cs**: Lida com morte do bot, eventos e notificações de respawn.
* **BotSpawner_Proto.cs**: Gerencia spawn e respawn de bots, tanto offline quanto online.
* **BotWeaponAutoAttach.cs**: Instancia e anexa armas ao bot.
* **BotRespawnLink.cs**: Liga bots ao spawner para respawn com waypoints preferidos.
* **BotDiagnostics.cs**: Ferramenta de debug para detetar mudanças de vida, colisões e comportamento.

## Conclusão

O sistema de IA combina:

* **Máquinas de estados** para lógica de comportamento modular.
* **Pathfinding baseado em NavMesh** para movimentação eficiente em ambientes complexos.
* **Heurísticas de combate e pickups** para decisões rápidas e adaptativas.

Esta abordagem resulta em bots com comportamento crível, desafiador e de fácil manutenção.
