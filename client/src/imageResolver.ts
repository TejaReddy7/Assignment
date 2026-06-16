const IMAGE_MAP: Record<string, string> = {};

function loadImages() {
  const modules = import.meta.glob('./assets/*.jpg', { eager: true }) as Record<string, { default: string }>;
  for (const path in modules) {
    const filename = path.split('/').pop()?.replace('.jpg', '') ?? '';
    IMAGE_MAP[filename] = modules[path].default;
  }
}
loadImages();

export function resolveImage(name: string, typeName: string, franchiseCode: string): string | null {
  const lowerName = name.toLowerCase();
  const fCode = franchiseCode.toLowerCase();

  if (typeName === 'Jersey') {
    const isAway = lowerName.includes('away');
    
    // Check if we have an away-specific jersey (e.g. jersey-mi-away)
    if (isAway && IMAGE_MAP[`jersey-${fCode}-away`]) {
      return IMAGE_MAP[`jersey-${fCode}-away`];
    }
    // Check for standard home jersey
    if (IMAGE_MAP[`jersey-${fCode}`]) {
      return IMAGE_MAP[`jersey-${fCode}`];
    }
    // Fallbacks if team specific doesn't exist
    return IMAGE_MAP['jersey-csk'] ?? null;
  }

  if (typeName === 'Cap') {
    // Exact mapping for the newly generated and existing caps
    const capMapping: Record<string, string> = {
      mi: 'cap-blue',
      dc: 'cap-blue',
      kkr: 'cap-purple',
      rcb: 'cap-red',
      pbks: 'cap-red',
      csk: 'cap-yellow',
      srh: 'cap-orange',
      rr: 'cap-pink',
      gt: 'cap-navy',
      lsg: 'cap-lightblue'
    };
    const capKey = capMapping[fCode] || 'cap-blue';
    return IMAGE_MAP[capKey] ?? null;
  }

  if (typeName === 'Flag') {
    // Exact mapping for flags
    const flagMapping: Record<string, string> = {
      mi: 'flag-blue',
      dc: 'flag-blue',
      rcb: 'flag-red',
      pbks: 'flag-red'
    };
    const flagKey = flagMapping[fCode] || 'flag'; // fallback to standard 'flag'
    return IMAGE_MAP[flagKey] ?? null;
  }

  const directMap: Record<string, string> = {
    AutographedPhoto: 'autograph',
    Memorabilia: 'poster'
  };
  
  if (directMap[typeName]) {
    return IMAGE_MAP[directMap[typeName]] ?? null;
  }

  const accessoryKeywords: Record<string, string> = {
    backpack: 'backpack', bag: 'backpack', ball: 'ball', bat: 'bat',
    bottle: 'bottle', water: 'bottle', helmet: 'helmet', hoodie: 'hoodie',
    mug: 'mug', cup: 'mug', wristband: 'wristband', band: 'wristband', poster: 'poster',
  };
  for (const [kw, imgKey] of Object.entries(accessoryKeywords)) {
    if (lowerName.includes(kw)) return IMAGE_MAP[imgKey] ?? null;
  }

  return null;
}
