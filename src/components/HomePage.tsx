import React, { useState, useRef, useEffect } from 'react';
import {
  Folder,
  Plus,
  Clock,
  FileText,
  Sparkles,
  Trash2,
  HardDrive,
  Upload,
  Copy,
  Search,
  MoreVertical,
  ChevronDown,
  Layers,
  ExternalLink,
  Edit2,
  Check,
  Download,
  RotateCcw,
  Undo2,
} from 'lucide-react';
import type { Project, Workspace } from '../types';
import { bankingTemplateElements, landingPageTemplateElements } from '../templates';
import { parseAndImportDesignFile } from '../utils/figmaImporter';

interface HomePageProps {
  projects: Project[];
  workspaces: Workspace[];
  activeWorkspaceId: string;
  onSelectWorkspace: (id: string) => void;
  onOpenProject: (project: Project) => void;
  onCreateNewProject: (templateElements?: any[], projectName?: string) => void;
  onDeleteProject: (id: string) => void;
  onMoveToBin?: (id: string) => void;
  onRestoreFromBin?: (id: string) => void;
  onPermanentDelete?: (id: string) => void;
  onEmptyBin?: () => void;
  onDuplicateProject: (id: string, targetWorkspaceId?: string) => void;
  onRenameProject: (id: string, newName: string) => void;
  onMoveProjectToWorkspace: (projectId: string, workspaceId: string) => void;
  onCreateWorkspace: (name: string, description: string, color: string) => void;
  onDeleteWorkspace: (id: string) => void;
  storageUsage: { usedBytes: number; quotaBytes: number; projectCount: number };
}

export const HomePage: React.FC<HomePageProps> = ({
  projects,
  workspaces,
  activeWorkspaceId,
  onSelectWorkspace,
  onOpenProject,
  onCreateNewProject,
  onDeleteProject,
  onMoveToBin,
  onRestoreFromBin,
  onPermanentDelete,
  onEmptyBin,
  onDuplicateProject,
  onRenameProject,
  onMoveProjectToWorkspace,
  onCreateWorkspace,
  onDeleteWorkspace,
  storageUsage,
}) => {
  const [activeNav, setActiveNav] = useState<'recent' | 'drafts' | 'all' | 'templates' | 'bin'>('recent');
  const [searchQuery, setSearchQuery] = useState('');
  const [workspaceMenuOpen, setWorkspaceMenuOpen] = useState(false);
  const [createWsModalOpen, setCreateWsModalOpen] = useState(false);
  const [newWsName, setNewWsName] = useState('');
  const [newWsDesc, setNewWsDesc] = useState('');
  const [newWsColor, setNewWsColor] = useState('#0D99FF');

  const [activeProjectMenuId, setActiveProjectMenuId] = useState<string | null>(null);
  const [menuDirection, setMenuDirection] = useState<'down' | 'up'>('down');
  const [renamingProjectId, setRenamingProjectId] = useState<string | null>(null);
  const [renameInput, setRenameInput] = useState('');
  const [toast, setToast] = useState<{ id: string; name: string } | null>(null);
  const toastTimerRef = useRef<any>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Close context menus on outside click or Escape
  useEffect(() => {
    const handleGlobalClick = () => {
      setActiveProjectMenuId(null);
      setWorkspaceMenuOpen(false);
    };
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setActiveProjectMenuId(null);
        setWorkspaceMenuOpen(false);
      }
    };
    window.addEventListener('click', handleGlobalClick);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('click', handleGlobalClick);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  const handleExportProject = (p: Project) => {
    const dataStr = 'data:application/octet-stream;charset=utf-8,' + encodeURIComponent(JSON.stringify(p, null, 2));
    const downloadAnchor = document.createElement('a');
    downloadAnchor.setAttribute('href', dataStr);
    downloadAnchor.setAttribute('download', `${p.name.toLowerCase().replace(/\s+/g, '_')}.kerf`);
    document.body.appendChild(downloadAnchor);
    downloadAnchor.click();
    downloadAnchor.remove();
  };

  const handleMoveFileToBin = (p: Project) => {
    setActiveProjectMenuId(null);
    if (onMoveToBin) {
      onMoveToBin(p.id);
    } else {
      onDeleteProject(p.id);
    }
    setToast({ id: p.id, name: p.name });
    if (toastTimerRef.current) clearTimeout(toastTimerRef.current);
    toastTimerRef.current = setTimeout(() => {
      setToast(null);
    }, 6000);
  };

  const handlePermanentDeleteFile = (p: Project) => {
    setActiveProjectMenuId(null);
    if (window.confirm(`Permanently delete "${p.name}"? This file will be completely erased and cannot be recovered.`)) {
      if (onPermanentDelete) {
        onPermanentDelete(p.id);
      } else {
        onDeleteProject(p.id);
      }
    }
  };

  const handleUndoMoveToBin = () => {
    if (toast && onRestoreFromBin) {
      onRestoreFromBin(toast.id);
    }
    setToast(null);
  };

  const activeWorkspace = workspaces.find((w) => w.id === activeWorkspaceId) || workspaces[0];

  // Separate active projects from bin/deleted projects
  const deletedProjects = projects.filter((p) => p.deleted === true);
  const activeProjects = projects.filter((p) => !p.deleted);

  // Filter projects by workspace, search, and nav tab
  const filteredProjects = activeNav === 'bin'
    ? deletedProjects.filter((p) => !searchQuery || p.name.toLowerCase().includes(searchQuery.toLowerCase()))
    : activeProjects.filter((p) => {
        if (searchQuery) {
          return p.name.toLowerCase().includes(searchQuery.toLowerCase());
        }
        if (activeNav === 'recent') {
          return true; // show all recent across workspaces
        }
        if (activeNav === 'drafts') {
          return p.workspaceId === 'ws-personal';
        }
        return p.workspaceId === activeWorkspaceId;
      });

  const handleStartRename = (p: Project, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setRenamingProjectId(p.id);
    setRenameInput(p.name);
    setActiveProjectMenuId(null);
  };

  const handleFinishRename = (id: string) => {
    if (renameInput.trim()) {
      onRenameProject(id, renameInput.trim());
    }
    setRenamingProjectId(null);
  };

  const handleImportFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      const importedProject = await parseAndImportDesignFile(file, activeWorkspaceId);
      onOpenProject(importedProject);
    } catch (err: any) {
      alert(err?.message || 'Invalid design file format. Please provide a valid .kerf, .figma, or JSON file.');
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
  };

  return (
    <div
      style={{
        display: 'flex',
        width: '100vw',
        height: '100vh',
        backgroundColor: '#18181A',
        color: '#E0E0E0',
        fontFamily: 'var(--font-main)',
        overflow: 'hidden',
      }}
    >
      {/* Hidden Import File Input */}
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleImportFile}
        accept=".kerf,.figma,.fig,.json"
        style={{ display: 'none' }}
      />

      {/* Left Sidebar */}
      <aside
        style={{
          width: '260px',
          backgroundColor: '#1E1E20',
          borderRight: '1px solid #2C2C2E',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'space-between',
        }}
      >
        <div>
          {/* Studio Profile Brand Header */}
          <div
            style={{
              padding: '18px 16px',
              borderBottom: '1px solid #2C2C2E',
              display: 'flex',
              alignItems: 'center',
              gap: '12px',
            }}
          >
            <img
              src="/kerf-icon.svg"
              alt="Kerf"
              width="34"
              height="34"
              style={{
                display: 'block',
                borderRadius: '8px',
                boxShadow: '0 4px 12px rgba(13, 153, 255, 0.25)',
              }}
            />
            <div>
              <div style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>Kerf Studio</div>
              <div style={{ fontSize: '11px', color: '#888' }}>Next-Gen Collaborative IDE</div>
            </div>
          </div>

          {/* Workspace Selector Dropdown */}
          <div style={{ padding: '12px 14px', position: 'relative' }}>
            <div style={{ fontSize: '10px', fontWeight: 600, color: '#888', textTransform: 'uppercase', marginBottom: '6px' }}>
              Current Workspace
            </div>
            <button
              onClick={() => setWorkspaceMenuOpen(!workspaceMenuOpen)}
              className="figma-btn"
              style={{
                width: '100%',
                padding: '8px 10px',
                backgroundColor: '#262628',
                border: '1px solid #333336',
                borderRadius: '6px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span
                  style={{
                    width: '10px',
                    height: '10px',
                    borderRadius: '50%',
                    backgroundColor: activeWorkspace?.color || '#0D99FF',
                  }}
                />
                <span style={{ fontSize: '12px', fontWeight: 600, color: '#FFF' }}>
                  {activeWorkspace?.name || 'Workspace'}
                </span>
              </div>
              <ChevronDown size={14} color="#888" />
            </button>

            {/* Workspace Dropdown Menu */}
            {workspaceMenuOpen && (
              <div
                className="figma-dropdown"
                style={{ width: 'calc(100% - 28px)', top: '68px', left: '14px', zIndex: 100 }}
              >
                <div style={{ padding: '4px 8px', fontSize: '10px', color: '#888', fontWeight: 600 }}>
                  SWITCH WORKSPACE
                </div>
                {workspaces.map((ws) => (
                  <div
                    key={ws.id}
                    className="figma-dropdown-item"
                    onClick={() => {
                      onSelectWorkspace(ws.id);
                      setWorkspaceMenuOpen(false);
                    }}
                    style={{
                      backgroundColor: ws.id === activeWorkspaceId ? 'rgba(13, 153, 255, 0.15)' : 'transparent',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <span
                        style={{
                          width: '8px',
                          height: '8px',
                          borderRadius: '50%',
                          backgroundColor: ws.color || '#0D99FF',
                        }}
                      />
                      <span>{ws.name}</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                      {ws.id === activeWorkspaceId && <Check size={13} color="#0D99FF" />}
                      {ws.id !== 'ws-personal' && (
                        <button
                          className="figma-btn"
                          style={{ padding: '2px', color: '#666' }}
                          title="Delete Workspace"
                          onClick={(e) => {
                            e.stopPropagation();
                            if (confirm(`Delete workspace "${ws.name}"?`)) {
                              onDeleteWorkspace(ws.id);
                            }
                          }}
                        >
                          <Trash2 size={12} />
                        </button>
                      )}
                    </div>
                  </div>
                ))}

                <div style={{ height: '1px', backgroundColor: '#333', margin: '4px 0' }} />

                <div
                  className="figma-dropdown-item"
                  onClick={() => {
                    setCreateWsModalOpen(true);
                    setWorkspaceMenuOpen(false);
                  }}
                  style={{ color: '#0D99FF', fontWeight: 600 }}
                >
                  <Plus size={13} />
                  <span>Create Workspace...</span>
                </div>
              </div>
            )}
          </div>

          {/* Navigation Links */}
          <nav style={{ padding: '8px 10px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
            <button
              className="figma-btn"
              onClick={() => setActiveNav('recent')}
              style={{
                width: '100%',
                justifyContent: 'flex-start',
                padding: '8px 12px',
                borderRadius: '6px',
                backgroundColor: activeNav === 'recent' ? 'rgba(13, 153, 255, 0.12)' : 'transparent',
                color: activeNav === 'recent' ? '#0D99FF' : '#AAA',
                fontWeight: activeNav === 'recent' ? 600 : 500,
              }}
            >
              <Clock size={15} />
              <span>Recents</span>
            </button>

            <button
              className="figma-btn"
              onClick={() => setActiveNav('drafts')}
              style={{
                width: '100%',
                justifyContent: 'flex-start',
                padding: '8px 12px',
                borderRadius: '6px',
                backgroundColor: activeNav === 'drafts' ? 'rgba(13, 153, 255, 0.12)' : 'transparent',
                color: activeNav === 'drafts' ? '#0D99FF' : '#AAA',
                fontWeight: activeNav === 'drafts' ? 600 : 500,
              }}
            >
              <FileText size={15} />
              <span>Drafts</span>
            </button>

            <button
              className="figma-btn"
              onClick={() => setActiveNav('all')}
              style={{
                width: '100%',
                justifyContent: 'flex-start',
                padding: '8px 12px',
                borderRadius: '6px',
                backgroundColor: activeNav === 'all' ? 'rgba(13, 153, 255, 0.12)' : 'transparent',
                color: activeNav === 'all' ? '#0D99FF' : '#AAA',
                fontWeight: activeNav === 'all' ? 600 : 500,
              }}
            >
              <Folder size={15} />
              <span>Workspace Files</span>
            </button>

            <button
              className="figma-btn"
              onClick={() => setActiveNav('templates')}
              style={{
                width: '100%',
                justifyContent: 'flex-start',
                padding: '8px 12px',
                borderRadius: '6px',
                backgroundColor: activeNav === 'templates' ? 'rgba(13, 153, 255, 0.12)' : 'transparent',
                color: activeNav === 'templates' ? '#0D99FF' : '#AAA',
                fontWeight: activeNav === 'templates' ? 600 : 500,
              }}
            >
              <Sparkles size={15} color="#F59E0B" />
              <span>Templates & Kits</span>
            </button>

            <div style={{ height: '1px', backgroundColor: '#2C2C2E', margin: '6px 4px' }} />

            <button
              className="figma-btn"
              onClick={() => setActiveNav('bin')}
              style={{
                width: '100%',
                justifyContent: 'space-between',
                padding: '8px 12px',
                borderRadius: '6px',
                backgroundColor: activeNav === 'bin' ? 'rgba(239, 68, 68, 0.14)' : 'transparent',
                color: activeNav === 'bin' ? '#EF4444' : '#AAA',
                fontWeight: activeNav === 'bin' ? 600 : 500,
                transition: 'all 0.15s ease',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Trash2 size={15} color={activeNav === 'bin' ? '#EF4444' : '#888'} />
                <span>Bin</span>
              </div>
              {deletedProjects.length > 0 && (
                <span
                  style={{
                    fontSize: '11px',
                    padding: '1px 7px',
                    borderRadius: '10px',
                    backgroundColor: activeNav === 'bin' ? '#EF4444' : '#2C2C30',
                    color: '#FFF',
                    fontWeight: 600,
                  }}
                >
                  {deletedProjects.length}
                </span>
              )}
            </button>
          </nav>
        </div>

        {/* Footer Database Storage Status */}
        <div style={{ padding: '16px', borderTop: '1px solid #2C2C2E', backgroundColor: '#18181A' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '6px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '11px', color: '#888' }}>
              <HardDrive size={13} color="#10B981" />
              <span>Device DB Storage</span>
            </div>
            <span style={{ fontSize: '11px', color: '#10B981', fontWeight: 600 }}>Active</span>
          </div>
          <div style={{ fontSize: '11px', color: '#AAA' }}>
            {formatBytes(storageUsage.usedBytes)} used • {storageUsage.projectCount} projects
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <main style={{ flex: 1, display: 'flex', flexDirection: 'column', overflowY: 'auto' }}>
        {/* Top Header Bar */}
        <header
          style={{
            height: '60px',
            borderBottom: '1px solid #2C2C2E',
            padding: '0 28px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#1E1E20',
          }}
        >
          {/* Search bar */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              backgroundColor: '#141416',
              border: '1px solid #2C2C2E',
              borderRadius: '8px',
              padding: '6px 14px',
              width: '360px',
            }}
          >
            <Search size={15} color="#888" style={{ marginRight: '8px' }} />
            <input
              type="text"
              placeholder="Search files, projects and templates..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                background: 'transparent',
                border: 'none',
                outline: 'none',
                color: '#FFF',
                fontSize: '12px',
                width: '100%',
              }}
            />
          </div>

          {/* Action CTAs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <button
              className="figma-btn"
              onClick={() => fileInputRef.current?.click()}
              style={{
                padding: '7px 14px',
                fontSize: '12px',
                backgroundColor: '#262628',
                border: '1px solid #38383B',
              }}
              title="Import .kerf or .figma design file"
            >
              <Upload size={14} />
              <span>Import .kerf / .figma</span>
            </button>

            <button
              className="figma-btn figma-btn-primary"
              onClick={() => onCreateNewProject()}
              style={{
                padding: '7px 16px',
                fontSize: '12px',
                boxShadow: '0 4px 14px rgba(13, 153, 255, 0.4)',
              }}
            >
              <Plus size={15} />
              <span>New Design File</span>
            </button>
          </div>
        </header>

        {/* Dashboard Body */}
        <div style={{ padding: '28px', flex: 1 }}>
          {/* Templates Showcase Banner (only for active views, hidden in Bin) */}
          {activeNav !== 'bin' ? (
            <div style={{ marginBottom: '32px' }}>
              <div style={{ fontSize: '14px', fontWeight: 600, color: '#FFF', marginBottom: '14px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Sparkles size={16} color="#F59E0B" />
                <span>Start with a Production-Ready Template</span>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
                {/* Template 1: Banking */}
                <div
                  onClick={() => onCreateNewProject(bankingTemplateElements, 'FinTech & Banking Mobile App')}
                  style={{
                    backgroundColor: '#202024',
                    border: '1px solid #2F2F34',
                    borderRadius: '10px',
                    padding: '16px',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                    boxShadow: '0 4px 16px rgba(0, 0, 0, 0.4)',
                  }}
                  className="layer-row"
                >
                  <div
                    style={{
                      height: '90px',
                      borderRadius: '6px',
                      background: 'linear-gradient(135deg, #0F172A 0%, #1E293B 100%)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: '12px',
                      border: '1px solid rgba(255, 255, 255, 0.08)',
                    }}
                  >
                    <div style={{ width: '40px', height: '60px', borderRadius: '8px', border: '2px solid #0D99FF', background: '#0284C7' }} />
                  </div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>FinTech Mobile App</div>
                  <div style={{ fontSize: '11px', color: '#888', marginTop: '2px' }}>Cards, balances & transaction list</div>
                </div>

                {/* Template 2: SaaS Landing */}
                <div
                  onClick={() => onCreateNewProject(landingPageTemplateElements, 'Modern SaaS Landing Page')}
                  style={{
                    backgroundColor: '#202024',
                    border: '1px solid #2F2F34',
                    borderRadius: '10px',
                    padding: '16px',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                    boxShadow: '0 4px 16px rgba(0, 0, 0, 0.4)',
                  }}
                  className="layer-row"
                >
                  <div
                    style={{
                      height: '90px',
                      borderRadius: '6px',
                      background: 'linear-gradient(135deg, #18181B 0%, #27272A 100%)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: '12px',
                      border: '1px solid rgba(255, 255, 255, 0.08)',
                    }}
                  >
                    <div style={{ width: '80px', height: '45px', borderRadius: '4px', border: '1.5px solid #10B981', background: '#064E3B' }} />
                  </div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>SaaS Landing Page</div>
                  <div style={{ fontSize: '11px', color: '#888', marginTop: '2px' }}>Hero, pricing, testimonials & CTA</div>
                </div>

                {/* Template 3: Blank Canvas */}
                <div
                  onClick={() => onCreateNewProject([], 'Blank Design File')}
                  style={{
                    backgroundColor: '#202024',
                    border: '1px solid #2F2F34',
                    borderRadius: '10px',
                    padding: '16px',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                    boxShadow: '0 4px 16px rgba(0, 0, 0, 0.4)',
                  }}
                  className="layer-row"
                >
                  <div
                    style={{
                      height: '90px',
                      borderRadius: '6px',
                      background: '#141416',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: '12px',
                      border: '1px dashed #444',
                    }}
                  >
                    <Plus size={24} color="#888" />
                  </div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>Blank Canvas</div>
                  <div style={{ fontSize: '11px', color: '#888', marginTop: '2px' }}>Start from scratch with infinite space</div>
                </div>

                {/* Template 4: UI Components Kit */}
                <div
                  onClick={() => onCreateNewProject(bankingTemplateElements.slice(0, 10), 'Design System UI Kit')}
                  style={{
                    backgroundColor: '#202024',
                    border: '1px solid #2F2F34',
                    borderRadius: '10px',
                    padding: '16px',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                    boxShadow: '0 4px 16px rgba(0, 0, 0, 0.4)',
                  }}
                  className="layer-row"
                >
                  <div
                    style={{
                      height: '90px',
                      borderRadius: '6px',
                      background: 'linear-gradient(135deg, #3B0764 0%, #1E1B4B 100%)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: '12px',
                      border: '1px solid rgba(255, 255, 255, 0.08)',
                    }}
                  >
                    <Layers size={24} color="#C084FC" />
                  </div>
                  <div style={{ fontSize: '13px', fontWeight: 600, color: '#FFF' }}>UI Kit & Design Tokens</div>
                  <div style={{ fontSize: '11px', color: '#888', marginTop: '2px' }}>Buttons, inputs, badges & dialogs</div>
                </div>
              </div>
            </div>
          ) : (
            /* Bin Banner */
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '20px 24px',
                backgroundColor: '#1E1E22',
                border: '1px solid #2F2F34',
                borderRadius: '12px',
                marginBottom: '28px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                <div
                  style={{
                    width: '42px',
                    height: '42px',
                    borderRadius: '10px',
                    backgroundColor: 'rgba(239, 68, 68, 0.14)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    border: '1px solid rgba(239, 68, 68, 0.3)',
                  }}
                >
                  <Trash2 size={20} color="#EF4444" />
                </div>
                <div>
                  <div style={{ fontSize: '16px', fontWeight: 600, color: '#FFF' }}>
                    Bin / Trash
                  </div>
                  <div style={{ fontSize: '12px', color: '#888', marginTop: '2px' }}>
                    Deleted files remain stored locally on your device. You can restore them anytime or permanently erase them.
                  </div>
                </div>
              </div>

              {deletedProjects.length > 0 && onEmptyBin && (
                <button
                  className="figma-btn"
                  onClick={() => {
                    if (window.confirm(`Empty bin? All ${deletedProjects.length} deleted file(s) will be permanently erased and cannot be recovered.`)) {
                      onEmptyBin();
                    }
                  }}
                  style={{
                    padding: '7px 14px',
                    fontSize: '12px',
                    backgroundColor: 'rgba(239, 68, 68, 0.12)',
                    color: '#EF4444',
                    border: '1px solid rgba(239, 68, 68, 0.3)',
                    borderRadius: '6px',
                    fontWeight: 600,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                  }}
                >
                  <Trash2 size={13} color="#EF4444" />
                  <span>Empty Bin</span>
                </button>
              )}
            </div>
          )}

          {/* Files List Header */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
            <div style={{ fontSize: '15px', fontWeight: 600, color: '#FFF' }}>
              {activeNav === 'bin'
                ? 'Deleted Files in Bin'
                : activeNav === 'recent'
                ? 'Recently Opened Files'
                : activeNav === 'drafts'
                ? 'Personal Drafts'
                : `${activeWorkspace?.name} Files`}
              <span style={{ fontSize: '12px', color: '#888', fontWeight: 400, marginLeft: '8px' }}>
                ({filteredProjects.length})
              </span>
            </div>
          </div>

          {/* Files Grid */}
          {filteredProjects.length === 0 ? (
            <div
              style={{
                padding: '60px 20px',
                textAlign: 'center',
                backgroundColor: '#1E1E22',
                borderRadius: '12px',
                border: '1px dashed #333',
              }}
            >
              {activeNav === 'bin' ? (
                <>
                  <div
                    style={{
                      width: '52px',
                      height: '52px',
                      borderRadius: '50%',
                      backgroundColor: '#26262B',
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: '14px',
                    }}
                  >
                    <Trash2 size={24} color="#666" />
                  </div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: '#FFF' }}>Your Bin is empty</div>
                  <div style={{ fontSize: '12px', color: '#888', marginTop: '4px' }}>
                    Files you delete will appear here so you can easily restore them at any time.
                  </div>
                </>
              ) : (
                <>
                  <FileText size={32} color="#666" style={{ marginBottom: '12px' }} />
                  <div style={{ fontSize: '14px', fontWeight: 600, color: '#FFF' }}>No files found in this view</div>
                  <div style={{ fontSize: '12px', color: '#888', marginTop: '4px', marginBottom: '16px' }}>
                    Create a new design file or pick a template above to get started.
                  </div>
                  <button className="figma-btn figma-btn-primary" onClick={() => onCreateNewProject()}>
                    <Plus size={14} /> New File
                  </button>
                </>
              )}
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '18px' }}>
              {filteredProjects.map((p) => {
                const totalElements = p.pages?.reduce((acc, pg) => acc + (pg.elements?.length || 0), 0) || 0;
                const updatedStr = p.updatedAt ? new Date(p.updatedAt).toLocaleDateString() : 'Today';
                const isRenaming = renamingProjectId === p.id;
                const ws = workspaces.find((w) => w.id === p.workspaceId) || activeWorkspace;
                const isInBin = activeNav === 'bin' || !!p.deleted;
                const isMenuOpen = activeProjectMenuId === p.id;

                return (
                  <div
                    key={p.id}
                    onClick={() => {
                      if (isInBin) {
                        if (window.confirm(`"${p.name}" is in the Bin. Would you like to restore it to edit?`)) {
                          onRestoreFromBin?.(p.id);
                        }
                      } else {
                        onOpenProject(p);
                      }
                    }}
                    style={{
                      backgroundColor: '#202024',
                      border: isInBin
                        ? '1px solid rgba(239, 68, 68, 0.25)'
                        : isMenuOpen
                        ? '1px solid #0D99FF'
                        : '1px solid #2F2F34',
                      borderRadius: '10px',
                      overflow: isMenuOpen ? 'visible' : 'hidden',
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      boxShadow: isMenuOpen
                        ? '0 8px 24px rgba(0, 0, 0, 0.6), 0 0 0 1px #0D99FF'
                        : '0 4px 16px rgba(0, 0, 0, 0.4)',
                      transition: 'transform 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease',
                      position: 'relative',
                      opacity: isInBin ? 0.85 : 1,
                      zIndex: isMenuOpen ? 50 : 1,
                    }}
                    className="layer-row"
                  >
                    {/* Visual File Thumbnail Preview */}
                    <div
                      style={{
                        height: '140px',
                        backgroundColor: '#141416',
                        borderBottom: '1px solid #28282D',
                        borderTopLeftRadius: '9px',
                        borderTopRightRadius: '9px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        position: 'relative',
                        background: 'radial-gradient(rgba(255, 255, 255, 0.06) 1px, transparent 1px)',
                        backgroundSize: '16px 16px',
                      }}
                    >
                      <div
                        style={{
                          width: '100px',
                          height: '70px',
                          backgroundColor: '#1E1E22',
                          borderRadius: '6px',
                          border: '1px solid #333',
                          display: 'flex',
                          flexDirection: 'column',
                          padding: '8px',
                          gap: '6px',
                        }}
                      >
                        <div style={{ width: '40%', height: '6px', background: isInBin ? '#EF4444' : '#0D99FF', borderRadius: '3px' }} />
                        <div style={{ width: '80%', height: '4px', background: '#444', borderRadius: '2px' }} />
                        <div style={{ width: '60%', height: '4px', background: '#333', borderRadius: '2px' }} />
                      </div>

                      {/* Top left badge */}
                      {isInBin ? (
                        <span
                          style={{
                            position: 'absolute',
                            top: '10px',
                            left: '10px',
                            backgroundColor: 'rgba(239, 68, 68, 0.2)',
                            backdropFilter: 'blur(4px)',
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '10px',
                            color: '#EF4444',
                            border: '1px solid rgba(239, 68, 68, 0.35)',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '4px',
                            fontWeight: 600,
                          }}
                        >
                          <Trash2 size={10} color="#EF4444" />
                          Deleted {p.deletedAt ? new Date(p.deletedAt).toLocaleDateString() : ''}
                        </span>
                      ) : (
                        <span
                          style={{
                            position: 'absolute',
                            top: '10px',
                            left: '10px',
                            backgroundColor: 'rgba(0, 0, 0, 0.65)',
                            backdropFilter: 'blur(4px)',
                            padding: '2px 8px',
                            borderRadius: '12px',
                            fontSize: '10px',
                            color: ws?.color || '#0D99FF',
                            border: '1px solid rgba(255, 255, 255, 0.1)',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '4px',
                          }}
                        >
                          <span style={{ width: '6px', height: '6px', borderRadius: '50%', backgroundColor: ws?.color || '#0D99FF' }} />
                          {ws?.name || 'Workspace'}
                        </span>
                      )}
                    </div>

                    {/* File Meta Footer */}
                    <div style={{ padding: '14px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ flex: 1, overflow: 'hidden' }}>
                        {isRenaming ? (
                          <input
                            type="text"
                            className="figma-input"
                            value={renameInput}
                            onChange={(e) => setRenameInput(e.target.value)}
                            onBlur={() => handleFinishRename(p.id)}
                            onKeyDown={(e) => e.key === 'Enter' && handleFinishRename(p.id)}
                            autoFocus
                            onClick={(e) => e.stopPropagation()}
                            style={{ height: '26px' }}
                          />
                        ) : (
                          <div
                            style={{
                              fontSize: '13px',
                              fontWeight: 600,
                              color: '#FFF',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                            title={p.name}
                          >
                            {p.name}
                          </div>
                        )}
                        <div style={{ fontSize: '11px', color: '#888', marginTop: '3px' }}>
                          {totalElements} layers • {isInBin ? 'Moved to Bin' : `Edited ${updatedStr}`}
                        </div>
                      </div>

                      {/* Actions */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }} onClick={(e) => e.stopPropagation()}>
                        {isInBin ? (
                          <>
                            {/* Restore Button */}
                            <button
                              className="figma-btn"
                              style={{
                                padding: '4px 10px',
                                fontSize: '11px',
                                color: '#10B981',
                                backgroundColor: 'rgba(16, 185, 129, 0.12)',
                                border: '1px solid rgba(16, 185, 129, 0.25)',
                                borderRadius: '4px',
                                fontWeight: 600,
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px',
                              }}
                              onClick={(e) => {
                                e.stopPropagation();
                                onRestoreFromBin?.(p.id);
                              }}
                              title="Restore file to workspace"
                            >
                              <RotateCcw size={12} />
                              <span>Restore</span>
                            </button>

                            {/* Permanent Delete Button */}
                            <button
                              className="figma-btn"
                              style={{
                                padding: '5px',
                                color: '#888',
                                borderRadius: '4px',
                                transition: 'all 0.15s ease',
                              }}
                              onMouseEnter={(e) => {
                                e.currentTarget.style.color = '#EF4444';
                                e.currentTarget.style.backgroundColor = 'rgba(239, 68, 68, 0.15)';
                              }}
                              onMouseLeave={(e) => {
                                e.currentTarget.style.color = '#888';
                                e.currentTarget.style.backgroundColor = 'transparent';
                              }}
                              onClick={(e) => {
                                e.stopPropagation();
                                handlePermanentDeleteFile(p);
                              }}
                              title="Delete Permanently"
                            >
                              <Trash2 size={13} />
                            </button>
                          </>
                        ) : (
                          <div style={{ position: 'relative' }}>
                            {/* Card Context Menu Trigger (Ellipses / Overflow Menu) */}
                            <button
                              className="figma-btn"
                              style={{
                                padding: '5px',
                                color: isMenuOpen ? '#FFF' : '#888',
                                backgroundColor: isMenuOpen ? 'rgba(255, 255, 255, 0.14)' : 'transparent',
                                border: isMenuOpen ? '1px solid rgba(255, 255, 255, 0.25)' : '1px solid transparent',
                                borderRadius: '4px',
                                transition: 'all 0.12s ease',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                              }}
                              onClick={(e) => {
                                e.stopPropagation();
                                if (isMenuOpen) {
                                  setActiveProjectMenuId(null);
                                } else {
                                  const rect = e.currentTarget.getBoundingClientRect();
                                  const spaceBelow = window.innerHeight - rect.bottom;
                                  setMenuDirection(spaceBelow < 320 ? 'up' : 'down');
                                  setActiveProjectMenuId(p.id);
                                }
                              }}
                              title="More options"
                            >
                              <MoreVertical size={14} />
                            </button>

                            {/* Card Context Dropdown */}
                            {isMenuOpen && (
                              <div
                                className="figma-dropdown"
                                style={{
                                  position: 'absolute',
                                  right: 0,
                                  left: 'auto',
                                  ...(menuDirection === 'up'
                                    ? { bottom: 'calc(100% + 6px)', top: 'auto', marginTop: 0, marginBottom: '4px' }
                                    : { top: 'calc(100% + 6px)', bottom: 'auto', marginTop: '4px', marginBottom: 0 }),
                                  width: '210px',
                                  maxHeight: '380px',
                                  overflowY: 'auto',
                                  zIndex: 1000,
                                  backgroundColor: '#1E1E22',
                                  border: '1px solid #38383E',
                                  borderRadius: '8px',
                                  boxShadow: '0 12px 32px rgba(0, 0, 0, 0.85), 0 0 0 1px rgba(255, 255, 255, 0.08)',
                                  padding: '5px',
                                }}
                                onClick={(e) => e.stopPropagation()}
                              >
                                {/* 1. Open File */}
                                <div
                                  className="figma-dropdown-item"
                                  onClick={() => {
                                    onOpenProject(p);
                                    setActiveProjectMenuId(null);
                                  }}
                                  style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                                >
                                  <ExternalLink size={13} />
                                  <span>Open File</span>
                                </div>

                                {/* 2. Rename */}
                                <div
                                  className="figma-dropdown-item"
                                  onClick={(e) => {
                                    setActiveProjectMenuId(null);
                                    handleStartRename(p, e);
                                  }}
                                  style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                                >
                                  <Edit2 size={13} />
                                  <span>Rename</span>
                                </div>

                                {/* 3. Duplicate */}
                                <div
                                  className="figma-dropdown-item"
                                  onClick={() => {
                                    onDuplicateProject(p.id);
                                    setActiveProjectMenuId(null);
                                  }}
                                  style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                                >
                                  <Copy size={13} />
                                  <span>Duplicate</span>
                                </div>

                                {/* 4. Export File (.kerf) */}
                                <div
                                  className="figma-dropdown-item"
                                  onClick={() => {
                                    handleExportProject(p);
                                    setActiveProjectMenuId(null);
                                  }}
                                  style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                                >
                                  <Download size={13} />
                                  <span>Export File (.kerf)</span>
                                </div>

                                {workspaces.length > 1 && (
                                  <>
                                    <div style={{ height: '1px', backgroundColor: '#2E2E34', margin: '4px 0' }} />
                                    <div style={{ padding: '4px 8px', fontSize: '10px', color: '#777', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                                      Workspace
                                    </div>
                                    {workspaces
                                      .filter((w) => w.id !== p.workspaceId)
                                      .map((w) => (
                                        <div
                                          key={`move-${w.id}`}
                                          className="figma-dropdown-item"
                                          onClick={() => {
                                            onMoveProjectToWorkspace(p.id, w.id);
                                            setActiveProjectMenuId(null);
                                          }}
                                          style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                                        >
                                          <span style={{ width: '6px', height: '6px', borderRadius: '50%', backgroundColor: w.color }} />
                                          <span>Move to {w.name}</span>
                                        </div>
                                      ))}
                                  </>
                                )}

                                <div style={{ height: '1px', backgroundColor: '#2E2E34', margin: '4px 0' }} />

                                {/* 5. Move to Bin (Soft delete) */}
                                <div
                                  className="figma-dropdown-item"
                                  style={{ color: '#F87171', display: 'flex', alignItems: 'center', gap: '8px' }}
                                  onClick={() => handleMoveFileToBin(p)}
                                  title="Move to Bin (can be restored later)"
                                >
                                  <Trash2 size={13} color="#F87171" />
                                  <span>Move to Bin</span>
                                </div>

                                {/* 6. Permanent Delete */}
                                <div
                                  className="figma-dropdown-item"
                                  style={{ color: '#EF4444', display: 'flex', alignItems: 'center', gap: '8px' }}
                                  onClick={() => handlePermanentDeleteFile(p)}
                                  title="Permanently remove file from device storage"
                                >
                                  <Trash2 size={13} color="#EF4444" />
                                  <span>Delete Permanently</span>
                                </div>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </main>

      {/* Create Workspace Modal */}
      {createWsModalOpen && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.7)',
            backdropFilter: 'blur(8px)',
            zIndex: 10000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
          onClick={() => setCreateWsModalOpen(false)}
        >
          <div
            style={{
              width: '460px',
              backgroundColor: '#1E1E20',
              border: '1px solid #333',
              borderRadius: '12px',
              padding: '24px',
              boxShadow: '0 24px 64px rgba(0, 0, 0, 0.8)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ fontSize: '15px', fontWeight: 600, color: '#FFF', marginBottom: '4px' }}>
              Create New Workspace
            </div>
            <div style={{ fontSize: '12px', color: '#888', marginBottom: '20px' }}>
              Workspaces help you organize projects, share design tokens, and collaborate.
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '11px', color: '#AAA', display: 'block', marginBottom: '6px' }}>
                  Workspace Name
                </label>
                <input
                  type="text"
                  placeholder="e.g. Marketing Design System"
                  className="figma-input"
                  value={newWsName}
                  onChange={(e) => setNewWsName(e.target.value)}
                />
              </div>

              <div>
                <label style={{ fontSize: '11px', color: '#AAA', display: 'block', marginBottom: '6px' }}>
                  Description (Optional)
                </label>
                <input
                  type="text"
                  placeholder="What is this workspace used for?"
                  className="figma-input"
                  value={newWsDesc}
                  onChange={(e) => setNewWsDesc(e.target.value)}
                />
              </div>

              <div>
                <label style={{ fontSize: '11px', color: '#AAA', display: 'block', marginBottom: '6px' }}>
                  Workspace Color Tag
                </label>
                <div style={{ display: 'flex', gap: '10px' }}>
                  {['#0D99FF', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899', '#06B6D4'].map((col) => (
                    <div
                      key={col}
                      onClick={() => setNewWsColor(col)}
                      style={{
                        width: '28px',
                        height: '28px',
                        borderRadius: '50%',
                        backgroundColor: col,
                        cursor: 'pointer',
                        border: newWsColor === col ? '2px solid #FFF' : '2px solid transparent',
                        transform: newWsColor === col ? 'scale(1.15)' : 'none',
                        transition: 'transform 0.15s ease',
                      }}
                    />
                  ))}
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '24px' }}>
              <button className="figma-btn" onClick={() => setCreateWsModalOpen(false)}>
                Cancel
              </button>
              <button
                className="figma-btn figma-btn-primary"
                disabled={!newWsName.trim()}
                onClick={() => {
                  onCreateWorkspace(newWsName.trim(), newWsDesc.trim(), newWsColor);
                  setNewWsName('');
                  setNewWsDesc('');
                  setCreateWsModalOpen(false);
                }}
              >
                Create Workspace
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Floating Undo Toast for Moved to Bin */}
      {toast && (
        <div
          style={{
            position: 'fixed',
            bottom: '24px',
            right: '24px',
            backgroundColor: '#202024',
            border: '1px solid #333338',
            borderRadius: '8px',
            padding: '10px 16px',
            boxShadow: '0 12px 36px rgba(0, 0, 0, 0.7)',
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            zIndex: 10000,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: '#FFF' }}>
            <Trash2 size={15} color="#EF4444" />
            <span>
              <strong>{toast.name}</strong> moved to Bin
            </span>
          </div>
          {onRestoreFromBin && (
            <button
              className="figma-btn"
              onClick={handleUndoMoveToBin}
              style={{
                padding: '4px 10px',
                fontSize: '12px',
                color: '#0D99FF',
                backgroundColor: 'rgba(13, 153, 255, 0.12)',
                border: '1px solid rgba(13, 153, 255, 0.3)',
                borderRadius: '4px',
                fontWeight: 600,
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
              }}
            >
              <Undo2 size={13} />
              <span>Undo</span>
            </button>
          )}
          <button
            className="figma-btn"
            onClick={() => setToast(null)}
            style={{ padding: '2px 4px', color: '#666', marginLeft: '4px', fontSize: '11px' }}
          >
            ✕
          </button>
        </div>
      )}
    </div>
  );
};
