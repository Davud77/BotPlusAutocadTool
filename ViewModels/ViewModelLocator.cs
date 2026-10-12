// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using BotPlusAutocadTool.Services;

namespace BotPlusAutocadTool.ViewModels
{
    /// <summary>
    /// ViewModelLocator for dependency injection and ViewModel management
    /// </summary>
    public class ViewModelLocator
    {
        private static ViewModelLocator _instance;
        private readonly IAutoCADService _autoCADService;
        private StampPropsViewModel _stampPropsViewModel;
        private AutoNumSettingsViewModel _autoNumSettingsViewModel;

        public static ViewModelLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ViewModelLocator();
                }
                return _instance;
            }
        }

        private ViewModelLocator()
        {
            _autoCADService = new AutoCADService();
        }

        public StampPropsViewModel StampPropsViewModel
        {
            get
            {
                if (_stampPropsViewModel == null)
                {
                    _stampPropsViewModel = new StampPropsViewModel(_autoCADService);
                }
                return _stampPropsViewModel;
            }
        }

        public AutoNumSettingsViewModel AutoNumSettingsViewModel
        {
            get
            {
                if (_autoNumSettingsViewModel == null)
                {
                    _autoNumSettingsViewModel = new AutoNumSettingsViewModel();
                }
                return _autoNumSettingsViewModel;
            }
        }
    }
}