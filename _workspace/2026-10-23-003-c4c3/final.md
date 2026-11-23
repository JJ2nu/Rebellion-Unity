Unity 기반 전략 퍼즐 게임 ‘Re:bellion’을 개발하며 AI에 구현 작업을 나누어 맡기고 설계와 검증을 주도했습니다. 기물 배치와 전투, 재시도가 서로 맞물려 있어 기능 하나를 수정할 때도 전체 흐름을 살펴야 했습니다. 저는 먼저 게임 규칙과 기능 간 의존성을 정리하고 작업을 나눌 기준과 진행 순서를 정했습니다.

Codex의 하네스를 조정해 이 기준을 실제 작업 흐름에 반영했습니다. Unity MCP를 연결해 코드와 씬·프리팹을 함께 확인하게 하고 구조 검토에는 추론 강도를 높였습니다. 반복적인 Unity 설정은 하위 모델에 맡기되 에이전트의 수정 범위와 재귀 생성은 제한해 각 단계의 결과를 제가 검토한 뒤 다음 작업을 지시했습니다. 이 작업 범위와 검토 기준을 커밋·푸시·PR에서도 유지하도록 절차를 개인 스킬로 정리하게 했습니다. 변경 범위 확인과 기존 작성 양식, 팀원 리뷰 요청을 규칙으로 남겨 다음 작업에도 재사용했습니다.

전투 연출에서는 ‘전투가 밋밋하다’는 피드백을 구체적인 작업으로 바꿨습니다. 기존 퍼즐 규칙에 영향을 주지 않도록 전투 판정과 연출을 분리하고 카메라 포커싱과 히트스톱을 단계적으로 적용하게 했습니다. 이후 직접 플레이하며 공격 흐름이 잘 보이는지 확인했습니다. 흔들림이 과하거나 타이밍이 어색하면 수정 기준을 제시하고 직접 조정했습니다.

기물 배치부터 전투와 재시도로 이어지는 플레이 흐름을 구현하고 연출을 보완하는 동안 AI는 코드 작성과 에디터 작업을 도왔습니다. 저는 어떤 기능을 어떤 순서로 만들지 설계하고 구현 결과가 게임 규칙과 플레이 의도에 맞는지 검증했습니다. 수정 방향과 최종 반영 여부도 제가 결정했습니다.

<!-- HUMANIZE-SUMMARY
{
  "status": "완료",
  "route": "heavy",
  "finalize_verdict": "accept",
  "original_characters": 840,
  "final_characters": 828,
  "character_count_basis": "앞뒤 공백 제거, 공백과 본문 내 줄바꿈 포함",
  "gate_change_rate": 0.1343,
  "grade": "A",
  "self_validation": {
    "passed": 6,
    "total": 6,
    "failed": [],
    "checks": {
      "facts_terms_anchors_preserved": true,
      "change_rate_under_30_percent": true,
      "genre_preserved": true,
      "register_preserved": true,
      "s1_and_triggered_d14_clear": true,
      "no_new_claims_metaphors_cliches": true
    }
  },
  "categories": [
    "C-1 구조적 가족 판정",
    "E-1 문장 리듬",
    "C-4 결말 재요약"
  ],
  "highlights": [
    {
      "before": "이 기준이 작업마다 유지되도록 Codex의 하네스를 프로젝트에 맞게 조정했습니다.",
      "after": "Codex의 하네스를 조정해 이 기준을 실제 작업 흐름에 반영했습니다."
    },
    {
      "before": "커밋·푸시·PR 절차는 개인 스킬로 정리하게 했습니다.",
      "after": "이 작업 범위와 검토 기준을 커밋·푸시·PR에서도 유지하도록 절차를 개인 스킬로 정리하게 했습니다."
    },
    {
      "before": "구현 결과가 게임 규칙과 플레이 의도에 맞는지 검증하고 수정 방향과 최종 반영 여부를 결정한 것도 제 역할이었습니다.",
      "after": "수정 방향과 최종 반영 여부도 제가 결정했습니다."
    }
  ],
  "residual_findings": [
    {
      "id": "E-1",
      "severity": "S2",
      "location": "전투 연출 문단",
      "finding": "비슷한 길이의 완료형 문장 일부 잔존. 사례의 단계와 합쇼체를 보존할 필요가 있어 추가 수정하지 않음."
    }
  ],
  "fidelity_violations": [],
  "overediting": [],
  "finalizer_corrections": 0
}
-->
